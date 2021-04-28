using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEngine.ResourceManagement.ResourceProviders;

/// <summary>
/// Separate catalog for assigned asset groups.
/// </summary>
[CreateAssetMenu(menuName = "Addressables/new Catalog Content Group", fileName = "NewCatalogContentGroup")]
public class CatalogContentGroup : ScriptableObject
{
	[SerializeField, Tooltip("Assets groups that belong to this catalog. Entries found in these will get extracted from the default catalog.")]
	private List<AddressableAssetGroup> assetGroups = new List<AddressableAssetGroup>();
	[SerializeField, Tooltip("Build path for the produced files associated with this catalog.")]
	private string buildPath = string.Empty;
	[SerializeField, Tooltip("Runtime load path for assets associated with this catalog.")]
	private string runtimeLoadPath = string.Empty;
	[SerializeField, Tooltip("Catalog name.")]
	private string catalogName = string.Empty;

	public string CatalogName
	{
		get { return catalogName; }
		set { catalogName = value; }
	}

	public string BuildPath
	{
		get { return buildPath; }
		set { buildPath = value; }
	}

	public string RuntimeLoadPath
	{
		get { return runtimeLoadPath; }
		set { runtimeLoadPath = value; }
	}

	public IReadOnlyList<AddressableAssetGroup> AssetGroups
	{
		get { return assetGroups; }
		set { new List<AddressableAssetGroup>(value); }
	}

	public bool IsPartOfCatalog(ContentCatalogDataEntry loc, AddressableAssetsBuildContext aaContext)
	{
		if ((assetGroups != null) && (assetGroups.Count > 0))
		{
			if ((loc.ResourceType == typeof(IAssetBundleResource)))
			{
				AddressableAssetEntry entry = aaContext.assetEntries.Find(ae => string.Equals(ae.BundleFileId, loc.InternalId));
				if (entry == null)
				{
					return false;
				}

				return assetGroups.Exists(ag => ag.entries.Contains(entry));
			}
			else
			{
				return assetGroups.Exists(ag => ag.entries.Any(e => loc.Keys.Contains(e.guid)));
			}
		}
		else
		{
			return false;
		}
	}
}
