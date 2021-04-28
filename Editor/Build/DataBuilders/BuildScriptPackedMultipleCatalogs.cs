using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.Initialization;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace UnityEditor.AddressableAssets.Build.DataBuilders
{
	/// <summary>
	/// Build script used for player builds and running with bundles in the editor, allowing building of multiple catalogs.
	/// </summary>
	[CreateAssetMenu(fileName = "BuildScriptPackedMultipleCatalogs.asset", menuName = "Addressables/Content Builders/Multiple Catalogs")]
	public class BuildScriptPackedMultipleCatalogs : BuildScriptPackedMode
	{
		/// <summary>
		/// Move a file, deleting it first if it exists.
		/// </summary>
		/// <param name="src">the file to move</param>
		/// <param name="dst">the destination</param>
		private static void FileMoveOverwrite(string src, string dst)
		{
			if (File.Exists(dst))
			{
				File.Delete(dst);
			}
			File.Move(src, dst);
		}

		[SerializeField]
		private List<CatalogContentGroup> additionalCatalogs = new List<CatalogContentGroup>();

		private readonly List<CatalogSetup> catalogSetups = new List<CatalogSetup>();

		public override string Name
		{
			get { return base.Name + " - Multiple Catalogs"; }
		}

		public List<CatalogContentGroup> AdditionalCatalogs
		{
			get { return additionalCatalogs; }
			set { additionalCatalogs = value; }
		}

		protected override List<ContentCatalogBuildInfo> GetContentCatalogs(AddressablesDataBuilderInput builderInput, AddressableAssetsBuildContext aaContext)
		{
			// cleanup
			catalogSetups.Clear();

			// Prepare catalogs
			var defaultCatalog = new ContentCatalogBuildInfo(ResourceManagerRuntimeData.kCatalogAddress, builderInput.RuntimeCatalogFilename);
			foreach (CatalogContentGroup catalogContentGroup in additionalCatalogs)
			{
				if (catalogContentGroup != null)
				{
					catalogSetups.Add(new CatalogSetup(catalogContentGroup));
				}
			}

			// Assign assets to new catalogs based on included groups
			foreach (var loc in aaContext.locations)
			{
				CatalogSetup preferredCatalog = catalogSetups.FirstOrDefault(cs => cs.CatalogContentGroup.IsPartOfCatalog(loc, aaContext));
				if (preferredCatalog != null)
				{
					if (loc.ResourceType == typeof(IAssetBundleResource))
					{
						string filePath = Path.GetFullPath(loc.InternalId.Replace("{UnityEngine.AddressableAssets.Addressables.RuntimePath}", Addressables.BuildPath));
						string runtimeLoadPath = preferredCatalog.CatalogContentGroup.RuntimeLoadPath + "/" + Path.GetFileName(filePath);

						preferredCatalog.Files.Add(filePath);
						preferredCatalog.BuildInfo.Locations.Add(new ContentCatalogDataEntry(typeof(IAssetBundleResource), runtimeLoadPath, loc.Provider, loc.Keys, loc.Dependencies, loc.Data));
					}
					else
					{
						preferredCatalog.BuildInfo.Locations.Add(loc);
					}
				}
				else
				{
					defaultCatalog.Locations.Add(loc);
				}
			}

			// Process dependencies
			foreach (CatalogSetup additionalCatalog in catalogSetups)
			{
				var dataEntries = new Queue<ContentCatalogDataEntry>(additionalCatalog.BuildInfo.Locations);
				var processedEntries = new HashSet<ContentCatalogDataEntry>();
				while (dataEntries.Count > 0)
				{
					ContentCatalogDataEntry dataEntry = dataEntries.Dequeue();
					if (!processedEntries.Add(dataEntry) || (dataEntry.Dependencies == null) || (dataEntry.Dependencies.Count == 0))
					{
						continue;
					}

					foreach (var entryDependency in dataEntry.Dependencies)
					{
						// Search for the dependencies in the default catalog only.
						var depLocation = defaultCatalog.Locations.Find(loc => loc.Keys[0] == entryDependency);
						if (depLocation != null)
						{
							dataEntries.Enqueue(depLocation);

							// If the dependency wasn't part of the catalog yet, add it.
							if (!additionalCatalog.BuildInfo.Locations.Contains(depLocation))
							{
								additionalCatalog.BuildInfo.Locations.Add(depLocation);
							}
						}
						else if (!additionalCatalog.BuildInfo.Locations.Exists(loc => loc.Keys[0] == entryDependency))
						{
							Debug.LogErrorFormat("Could not find location for dependency ID {0} in the default catalog.", entryDependency);
						}
					}
				}
			}

			// Gather catalogs
			var catalogs = new List<ContentCatalogBuildInfo>(catalogSetups.Count + 1);
			catalogs.Add(defaultCatalog);
			foreach (var setup in catalogSetups)
			{
				if (!setup.Empty)
				{
					catalogs.Add(setup.BuildInfo);
				}
			}
			return catalogs;
		}

		protected override TResult DoBuild<TResult>(AddressablesDataBuilderInput builderInput, AddressableAssetsBuildContext aaContext)
		{
			// execute build script
			var result = base.DoBuild<TResult>(builderInput, aaContext);

			// move extra catalogs to CatalogsBuildPath
			foreach (var setup in catalogSetups)
			{
				// Empty catalog setups are not added/built
				if (setup.Empty)
				{
					continue;
				}

				var bundlePath = aaContext.Settings.profileSettings.EvaluateString(aaContext.Settings.activeProfileId, setup.CatalogContentGroup.BuildPath);
				Directory.CreateDirectory(bundlePath);

				FileMoveOverwrite(Path.Combine(Addressables.BuildPath, setup.BuildInfo.JsonFilename), Path.Combine(bundlePath, setup.BuildInfo.JsonFilename));
				foreach (var file in setup.Files)
				{
					FileMoveOverwrite(file, Path.Combine(bundlePath, Path.GetFileName(file)));
				}
			}

			return result;
		}

		public override void ClearCachedData()
		{
			base.ClearCachedData();

			if ((additionalCatalogs == null) || (additionalCatalogs.Count == 0))
			{
				return;
			}

			// Cleanup the additional catalogs
			AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
			foreach (CatalogContentGroup additionalCatalog in additionalCatalogs)
			{
				string buildPath = settings.profileSettings.EvaluateString(settings.activeProfileId, additionalCatalog.BuildPath);
				if (!Directory.Exists(buildPath))
				{
					continue;
				}

				foreach (string catalogFile in Directory.GetFiles(buildPath))
				{
					File.Delete(catalogFile);
				}

				Directory.Delete(buildPath, true);
			}
		}

		private class CatalogSetup
		{
			public readonly CatalogContentGroup CatalogContentGroup = null;

			/// <summary>
			/// The catalog build info.
			/// </summary>
			public readonly ContentCatalogBuildInfo BuildInfo;

			/// <summary>
			/// The files associated to the catalog.
			/// </summary>
			public readonly List<string> Files = new List<string>(1);

			/// <summary>
			/// Tells whether the catalog is empty.
			/// </summary>
			public bool Empty
			{
				get { return BuildInfo.Locations.Count == 0; }
			}

			public CatalogSetup(CatalogContentGroup buildCatalog)
			{
				this.CatalogContentGroup = buildCatalog;
				BuildInfo = new ContentCatalogBuildInfo(buildCatalog.CatalogName, buildCatalog.CatalogName + ".json");
				BuildInfo.Register = false;
			}
		}
	}
}
