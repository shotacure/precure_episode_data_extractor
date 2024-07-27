using System;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using CsvHelper;
using HtmlAgilityPack;

/// <summary>
/// プリキュア話数データ抽出
/// </summary>
class PrecureEpisodeDataExtractor
{
    /// <summary>
    /// メイン
    /// </summary>
    /// <param name="args"></param>
    /// <returns></returns>
    static async Task Main(string[] args)
    {
        // シリーズ
        var series = new Dictionary<int, string>
        {
            { 2004, "precure" },
            { 2005, "precure_MH" },
            { 2006, "precure_SS" },
            { 2007, "yes_precure5" },
            { 2008, "precure5_gogo" },
            { 2009, "fresh_precure" },
            { 2010, "hc_precure" },
            { 2011, "suite_precure" },
            { 2012, "smile_precure" },
            { 2013, "dd_precure" },
            { 2014, "happinesscharge_precure" },
            { 2015, "princess_precure" },
            { 2016, "mahotsukai_precure" },
            { 2017, "precure_alamode" },
            { 2018, "hugtto_precure" },
            { 2019, "startwinkle_precure" },
            { 2020, "healingood_precure" },
            { 2021, "tropical-rouge_precure" },
            { 2022, "delicious-party_precure" },
            { 2023, "hirogaru-sky_precure" },
            { 2024, "wonderful_precure" }
        };

        var episodes = new List<Episode>();

        // 各シリーズの話数ページを取得
        foreach (var year in series.Keys)
        {
            var baseUrl = $"https://lineup.toei-anim.co.jp/ja/tv/{series[year]}/episode/";
            var maxPages = await GetMaxPages(baseUrl);

            for (int i = 1; i <= maxPages; i++)
            {
                var url = $"{baseUrl}{i}/";
                try
                {
                    var episode = await ExtractEpisodeData(url);
                    episode.年度 = year;
                    episodes.Add(episode);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"データを取得できませんでした {url}: {e.Message}");
                }
            }
        }

        using (var writer = new StreamWriter("precure_episodes.csv"))
        using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
        {
            csv.WriteRecords(episodes);
        }
    }

    /// <summary>
    /// 全話数を取得する
    /// </summary>
    /// <param name="baseUrl">ベースURL</param>
    /// <returns>全話数</returns>
    static async Task<int> GetMaxPages(string baseUrl)
    {
        var httpClient = new HttpClient();
        for (int i = 1; ; i++)
        {
            var url = $"{baseUrl}{i}/";
            var response = await httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return i - 1;
            }
        }
    }

    /// <summary>
    /// 話数データを抽出する
    /// </summary>
    /// <param name="url">URL</param>
    /// <returns>話数インスタンス</returns>
    static async Task<Episode> ExtractEpisodeData(string url)
    {
        var httpClient = new HttpClient();
        var response = await httpClient.GetStringAsync(url);
        var doc = new HtmlDocument();
        doc.LoadHtml(response);

        // 放送データ
        var episodeData = doc.DocumentNode.SelectSingleNode("//div[@id='story_caption']");
        var titleNode = episodeData.SelectSingleNode(".//h1[@class='story_caption_title']");

        // 話数
        var episodeIdNode = titleNode.SelectSingleNode(".//span[@class='episode_id']");
        var episodeId = Regex.Match(episodeIdNode.InnerText.Trim(), @"\d+").Value;

        // サブタイトル
        var subtitle = episodeIdNode.NextSibling.InnerText.Trim();
        subtitle = subtitle.Replace("【", "").Replace("】", "");
        
        // はぐプリ対策、最初と最後のカギ括弧を削除
        if (subtitle.StartsWith("「") && subtitle.EndsWith("」"))
        {
            subtitle = subtitle.Substring(1, subtitle.Length - 2);
        }

        // 放送日時
        var dateNode = titleNode.SelectSingleNode(".//following-sibling::p/span");
        var broadcastDate = dateNode.PreviousSibling.InnerText.Trim();
        var broadcastDateTime = $"{broadcastDate} 08:30";

        // 脚本
        var script = ExtractTextBetween(episodeData, "脚本：", "<").Trim();

        // 絵コンテ
        var storyboard = ExtractTextBetween(episodeData, "絵コンテ：", "<", true).Trim();
        storyboard = storyboard.Replace("絵コンテ：", "").Trim(); // ヒープリ中盤以降の変な表記に対応して「絵コンテ：」重複を削除

        // 演出
        var direction = ExtractTextBetween(episodeData, "演出：", "<").Trim();

        // 作画監督
        var animation = ExtractTextBetween(episodeData, "作画：", "<").Trim();

        // 美術
        var art = ExtractTextBetween(episodeData, "美術：", "<").Trim();

        return new Episode
        {
            話数 = episodeId,
            サブタイトル = subtitle,
            放送日時 = broadcastDateTime,
            脚本 = script,
            絵コンテ = storyboard,
            演出 = direction,
            作画監督 = animation,
            美術 = art
        };
    }

    /// <summary>
    /// 任意のテキストで括られた文字列を抽出する
    /// </summary>
    /// <param name="node">HTMLノード</param>
    /// <param name="startText">開始テキスト</param>
    /// <param name="endText">終了テキスト</param>
    /// <param name="allowEmpty">空を許容する</param>
    /// <returns>任意のテキストで括られた文字列</returns>
    static string ExtractTextBetween(HtmlNode node, string startText, string endText, bool allowEmpty = false)
    {
        var text = node.InnerHtml;
        var startIndex = text.IndexOf(startText);
        if (startIndex == -1)
        {
            return allowEmpty ? string.Empty : throw new Exception($"Text '{startText}' not found.");
        }
        startIndex += startText.Length;
        var endIndex = text.IndexOf(endText, startIndex);
        if (endIndex == -1)
        {
            return allowEmpty ? string.Empty : throw new Exception($"End text '{endText}' not found.");
        }
        return text.Substring(startIndex, endIndex - startIndex).Trim();
    }
}

/// <summary>
/// 話数クラス
/// </summary>
public class Episode
{
    public int 年度 { get; set; }
    public string 話数 { get; set; }
    public string サブタイトル { get; set; }
    public string 放送日時 { get; set; }
    public string 脚本 { get; set; }
    public string 絵コンテ { get; set; }
    public string 演出 { get; set; }
    public string 作画監督 { get; set; }
    public string 美術 { get; set; }
}