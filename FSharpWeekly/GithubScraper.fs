module GithubScraper

open System
open System.Linq
open System.Net.Http
open HtmlAgilityPack
open HtmlAgilityPack.CssSelectors.NetCore
open Types

let readRepositoryInfo (node: HtmlNode) : GithubRepository option = 
    match isNull node with 
    | true -> 
        None
    | false ->
        try 
           let allAnchors = node.QuerySelectorAll("a")
           // scrape the url of the repo
           let repo = allAnchors.First().Attributes.["href"].Value
           let repoParts = repo.Split([| '/' |]) |> Array.filter (fun part -> part <> "")
           let name = repoParts.[1]
           let owner = repoParts.[0]
           let url = sprintf "https://github.com%s" repo
           let description = node.QuerySelector(".py-1").InnerText.Trim()
           let stars = node.QuerySelectorAll("a.muted-link").First().InnerText.Trim().Replace("\n", "")
           let githubRepo = {
             Name = name 
             Url = url
             Description = description
             Owner = owner
             StarCount = int stars
           }

           Some githubRepo
        with 
        | ex -> None            

let loadRepos (htmlDoc: HtmlDocument)  = 
    let reposList = htmlDoc.DocumentNode.QuerySelector("ol.repo-list")
    reposList.QuerySelectorAll("li.col-12")
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