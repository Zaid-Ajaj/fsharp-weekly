module GithubScraper

open System
open System.Linq
open System.Net.Http
open HtmlAgilityPack
open HtmlAgilityPack.CssSelectors.NetCore
open Types
open System.Globalization

let readRepositoryInfo (node: HtmlNode) : GithubRepository option = 
    match isNull node with 
    | true -> 
        None
    | false ->
        try
           let anchor = node.QuerySelector("h1.lh-condensed").QuerySelector("a")
           // scrape the url of the repo
           let repo = anchor.Attributes.["href"].Value
           let repoParts = repo.Split([| '/' |]) |> Array.filter (fun part -> part <> "")
           let name = repoParts.[1]
           let owner = repoParts.[0]
           let url = sprintf "https://github.com%s" repo
           
           let description = 
              let p = node.QuerySelector("p.pr-4")
              if p = null then "" else p.InnerText.Trim()
           
           let sstars = node.QuerySelectorAll("a.mr-3").First().InnerText.Trim().Replace("\n", "")
           let r, stars = Int32.TryParse(sstars, NumberStyles.AllowThousands, CultureInfo.InvariantCulture)
           let stars' = if r then stars else -1

           let githubRepo = {
             Name = name 
             Url = url
             Description = description
             Owner = owner
             StarCount = stars'
           }

           Some githubRepo
        with 
        | ex -> None            

let loadRepos (htmlDoc: HtmlDocument)  = 
    htmlDoc.DocumentNode.QuerySelectorAll("article.Box-row")
    |> Seq.choose readRepositoryInfo
    |> List.ofSeq
     
let trendy (since: string) : Async<GithubRepository list> = 
    async {
        let url = "https://github.com/trending/f%23?since=" + since
        use httpClient = new HttpClient()
        let! content = Async.AwaitTask (httpClient.GetStringAsync(url))
        let document = HtmlDocument()
        do document.LoadHtml(content)
        return loadRepos document
    }

let trendyToday() = trendy "daily"
let trendyThisWeek() = trendy "weekly"