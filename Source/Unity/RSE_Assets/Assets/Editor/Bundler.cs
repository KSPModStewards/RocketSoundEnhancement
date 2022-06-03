using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class Bundler
{
    const string dir = "AssetBundles";
    [MenuItem("Bundler/Build Bundles")]
    static void BuildAssetBundles()
    {
        BuildPipeline.BuildAssetBundles(dir, BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.ForceRebuildAssetBundle, BuildTarget.StandaloneWindows);
    }
}

