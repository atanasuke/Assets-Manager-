// File: Assets/Editor/SortAPI.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public enum SortKey
{
    ProductName = 0,
    ShopName = 1,
    PublishedDate = 2,
    DownloadedDate = 3
}

public static class SortAPI
{
    private static SortKey _current = SortKey.ProductName;
    private static string _selectedSubCategory = null; // null = 全体
    private static string _selectedExtraCategory = null; // null = 指定なし

    public static SortKey GetCurrent() => _current;
    public static void SetCurrent(SortKey key) => _current = key;

    public static string GetSelectedSubCategory() => _selectedSubCategory;
    public static void SetSelectedSubCategory(string sub)
    {
        _selectedSubCategory = string.IsNullOrEmpty(sub) || sub == "すべて" ? null : sub;
        _selectedExtraCategory = null;
    }
    public static string GetSelectedExtraCategory() => _selectedExtraCategory;
    public static void SetSelectedExtraCategory(string extra) { _selectedExtraCategory = string.IsNullOrEmpty(extra) ? null : extra; }

    // サブカテゴリ → エクストラの定義
    private static readonly Dictionary<string, string[]> EXTRA_MAP = new Dictionary<string, string[]>
    {
        { "3D衣装", new[]{ "フル衣装", "髪型", "上着・パーカー", "ズボン・スカート", "靴・シューズ", "ネックレス・イヤリング", "メガネ・サングラス", "その他" } },
        { "3D装飾品", new[]{ "髪型", "リボン・ヘアピン", "ネイル", "アクセサリー", "ヘイロー", "その他" } },
        { "3D小道具", new[]{ "武器", "銃器", "タバコ", "ギミック", "ワールド向け", "その他" } },
        { "3Dテクスチャ", new[]{ "アイテクスチャ", "メイクアップ", "マテリアル", "マットキャップ", "しっぽ", "その他" } },
        { "3Dツール・システム", new[]{ "unity拡張ツール", "アバターギミック", "その他" } },
        { "3Dモーション・アニメーション", new[]{ "アバターギミック", "ダンス", "ポーズ", "表情アニメーション", "その他" } },
        { "3D環境・ワールド", new[]{ "ワールド", "システム", "ギミック小道具", "小道具・家具", "その他" } },
    };

    public static IReadOnlyDictionary<string, string[]> GetExtraMap() => EXTRA_MAP;

    // 推奨：インデックス順を返す（将来のグルーピングにも対応しやすい）
    public static List<int> GetSortedIndices(List<PackageAPIEntry> packages)
    {
        if (packages == null || packages.Count == 0)
            return new List<int>();

        IEnumerable<PackageAPIEntry> ordered = packages;
        switch (_current)
        {
            case SortKey.ProductName:
                ordered = packages.OrderBy(p => ReadMetaSafe(p)?.productName ?? p.FolderName, StringComparer.OrdinalIgnoreCase);
                break;
            case SortKey.ShopName:
                ordered = packages.OrderBy(p => ReadMetaSafe(p)?.shopName ?? p.FolderName, StringComparer.OrdinalIgnoreCase);
                break;
            case SortKey.PublishedDate:
                ordered = packages.OrderByDescending(p => ReadMetaSafe(p)?.publishedTimestamp ?? 0);
                break;
            case SortKey.DownloadedDate:
                ordered = packages.OrderByDescending(p => ReadMetaSafe(p)?.currentTimestamp ?? 0);
                break;
        }
        return ordered.Select(p => p.Index).ToList();
    }

    // 現在のフィルタに合致するインデックス
    public static List<int> GetFilteredIndices(List<PackageAPIEntry> packages)
    {
        if (packages == null || packages.Count == 0) return new List<int>();
        bool filterSub = !string.IsNullOrEmpty(_selectedSubCategory);
        bool filterExtra = !string.IsNullOrEmpty(_selectedExtraCategory);
        var result = new List<int>(packages.Count);
        foreach (var p in packages)
        {
            var meta = ReadMetaSafe(p);
            if (filterSub && !string.Equals(meta?.subCategory, _selectedSubCategory))
                continue;
            if (filterExtra)
            {
                if (meta?.extraCategories == null) continue;
                if (!meta.extraCategories.Any(c => string.Equals(c, _selectedExtraCategory)))
                    continue;
            }
            result.Add(p.Index);
        }
        return result;
    }

    // 追加のフィルタ: インポート状態 / 情報欠損
    private static ImportFilter _importFilter = ImportFilter.All;
    public static ImportFilter GetImportFilter() => _importFilter;
    public static void SetImportFilter(ImportFilter f) => _importFilter = f;

    public static List<int> ApplyImportStateFilter(List<PackageAPIEntry> packages, List<int> indices)
    {
        if (packages == null) return new List<int>();
        if (indices != null && indices.Count == 0)
        {
            // 前段のカテゴリ絞り込みが0件なら、そのまま0件を維持
            return new List<int>();
        }
        var set = new HashSet<int>(indices ?? new List<int>());
        var filtered = new List<int>();
        foreach (var p in packages)
        {
            if (indices != null && indices.Count > 0 && !set.Contains(p.Index)) continue;
            bool imported = ImportStatusAPI.IsImported(p.FolderPath);
            bool infoMissing = string.IsNullOrEmpty(p.MetaPath);
            switch (_importFilter)
            {
                case ImportFilter.All:
                    filtered.Add(p.Index); break;
                case ImportFilter.Imported:
                    if (imported) filtered.Add(p.Index); break;
                case ImportFilter.NotImported:
                    if (!imported) filtered.Add(p.Index); break;
                case ImportFilter.InfoMissing:
                    if (infoMissing) filtered.Add(p.Index); break;
            }
        }
        return filtered;
    }

    public static void GetImportFilterOptionsWithCounts(List<PackageAPIEntry> packages, List<int> categoryFilteredIndices, out string[] labels, out ImportFilter[] values)
    {
        int all = 0, imp = 0, notImp = 0, missing = 0;
        HashSet<int> baseSet = (categoryFilteredIndices != null && categoryFilteredIndices.Count > 0)
            ? new HashSet<int>(categoryFilteredIndices)
            : null;
        foreach (var p in packages)
        {
            if (baseSet != null && !baseSet.Contains(p.Index)) continue;
            bool imported = ImportStatusAPI.IsImported(p.FolderPath);
            bool infoMissing = string.IsNullOrEmpty(p.MetaPath);
            all++;
            if (infoMissing) missing++;
            if (imported) imp++; else notImp++;
        }
        labels = new string[]
        {
            $"すべて（{all}）",
            $"インポート済み（{imp}）",
            $"未インポート（{notImp}）",
            $"情報欠損（{missing}）"
        };
        values = new ImportFilter[]
        {
            ImportFilter.All,
            ImportFilter.Imported,
            ImportFilter.NotImported,
            ImportFilter.InfoMissing
        };
    }

    // サブカテゴリのドロップダウン用（ラベルと値の並行配列）
    public static void GetSubCategoryOptionsWithCounts(List<PackageAPIEntry> packages, out string[] labels, out string[] values)
    {
        // インポート状態を先に適用した集合でカウント
        var baseSet = ProjectToPackages(packages, ApplyImportStateFilter(packages, null));
        var keys = EXTRA_MAP.Keys.ToList();
        int total = baseSet.Count;
        var lbls = new List<string> { $"すべて（{total}）" };
        var vals = new List<string> { null };
        foreach (var key in keys)
        {
            int count = CountPackagesBySub(baseSet, key);
            lbls.Add($"{key}（{count}）");
            vals.Add(key);
        }
        labels = lbls.ToArray();
        values = vals.ToArray();
    }

    public static void GetExtraCategoryOptionsWithCounts(List<PackageAPIEntry> packages, string subCategory, out string[] labels, out string[] values)
    {
        // インポート状態を先に適用
        var baseSet = ProjectToPackages(packages, ApplyImportStateFilter(packages, null));

        if (string.IsNullOrEmpty(subCategory) || !EXTRA_MAP.ContainsKey(subCategory))
        {
            labels = new string[] { $"指定なし（{baseSet.Count}）" };
            values = new string[] { null };
            return;
        }

        var extras = EXTRA_MAP[subCategory];
        var listLabels = new List<string>();
        var listValues = new List<string>();

        int totalInSub = CountPackagesBySub(baseSet, subCategory);
        listLabels.Add($"指定なし（{totalInSub}）");
        listValues.Add(null);

        foreach (var ex in extras)
        {
            int c = CountPackagesBySubAndExtra(baseSet, subCategory, ex);
            listLabels.Add($"{ex}（{c}）");
            listValues.Add(ex);
        }
        labels = listLabels.ToArray();
        values = listValues.ToArray();
    }

    private static int CountPackagesBySub(List<PackageAPIEntry> packages, string sub)
    {
        int cnt = 0;
        foreach (var p in packages)
        {
            var m = ReadMetaSafe(p);
            if (m == null) continue;
            if (string.Equals(m.subCategory, sub)) cnt++;
        }
        return cnt;
    }

    private static int CountPackagesBySubAndExtra(List<PackageAPIEntry> packages, string sub, string extra)
    {
        int cnt = 0;
        foreach (var p in packages)
        {
            var m = ReadMetaSafe(p);
            if (m == null) continue;
            if (!string.Equals(m.subCategory, sub)) continue;
            if (m.extraCategories != null && m.extraCategories.Any(c => string.Equals(c, extra))) cnt++;
        }
        return cnt;
    }

    private static List<PackageAPIEntry> ProjectToPackages(List<PackageAPIEntry> all, List<int> indices)
    {
        if (all == null) return new List<PackageAPIEntry>();
        if (indices == null) return new List<PackageAPIEntry>();
        var set = new HashSet<int>(indices);
        var res = new List<PackageAPIEntry>();
        foreach (var p in all)
        {
            if (set.Contains(p.Index)) res.Add(p);
        }
        return res;
    }

    public static bool HasActiveCategoryFilter()
    {
        return !string.IsNullOrEmpty(_selectedSubCategory) || !string.IsNullOrEmpty(_selectedExtraCategory);
    }

    public static List<PackageAPIEntry> Sort(List<PackageAPIEntry> packages)
    {
        if (packages == null || packages.Count == 0) return packages;

        switch (_current)
        {
            case SortKey.ProductName:
                return packages.OrderBy(p => ReadMetaSafe(p)?.productName ?? p.FolderName, StringComparer.OrdinalIgnoreCase).ToList();
            case SortKey.ShopName:
                return packages.OrderBy(p => ReadMetaSafe(p)?.shopName ?? p.FolderName, StringComparer.OrdinalIgnoreCase).ToList();
            case SortKey.PublishedDate:
                // 新しい順
                return packages.OrderByDescending(p => ReadMetaSafe(p)?.publishedTimestamp ?? 0).ToList();
            case SortKey.DownloadedDate:
                // 新しい順
                return packages.OrderByDescending(p => ReadMetaSafe(p)?.currentTimestamp ?? 0).ToList();
            default:
                return packages;
        }
    }

    private static readonly Dictionary<string, MetaInfo> _metaCache = new Dictionary<string, MetaInfo>();

    private static MetaInfo ReadMetaSafe(PackageAPIEntry entry)
    {
        try
        {
            if (entry == null || string.IsNullOrEmpty(entry.MetaPath) || !File.Exists(entry.MetaPath))
                return null;

            if (_metaCache.TryGetValue(entry.MetaPath, out var cached))
                return cached;

            var json = File.ReadAllText(entry.MetaPath);
            var meta = JsonUtility.FromJson<MetaInfo>(json);
            _metaCache[entry.MetaPath] = meta;
            return meta;
        }
        catch
        {
            return null;
        }
    }
}


