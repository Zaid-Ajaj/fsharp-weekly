module Scraper

open Types
open System
open System.Net.Http
open HtmlAgilityPack
open HtmlAgilityPack.CssSelectors.NetCore

let formatText (value: string) =     
    value.Trim().Replace("&nbsp;", " ")
                .Replace("&quot;", "\"")
                .Replace("&#39;","'")
                .Replace("&#8220;", "“")
                .Replace("&#8221;","”")
                .Replace("&#8211;", "–")
                .Replace("&amp;","&")
                .Replace("&#8217;", "’")
                .Replace("&#8216;", "‘")

let extractLinks (links: HtmlNode) : Link list = 
    [ for link in links.QuerySelectorAll("li") -> 
        let url = link.QuerySelector("a").GetAttributeValue("href", "")
        { Content = formatText link.InnerText; Url = url } ]
    |> List.filter (fun link -> not (String.IsNullOrWhiteSpace link.Url))

let getArticleCategories (article: HtmlNode) : Category list = 
    let categoryNames = article.QuerySelectorAll("p > strong")
    let categoryLinks = article.QuerySelectorAll("ul")
    categoryLinks
    |> Seq.zip categoryNames
    |> Seq.map (fun (name, links) -> { Name = formatText name.InnerText; Links = extractLinks links })
    |> Seq.toList

let titleAndWeeknumber (article: HtmlNode) = 
    let header = article.QuerySelector(".entry-title")
    if isNull header
    then None 
    else 
       match header.InnerText.Split([| '–' |]) with 
       | [| weekNumber; title |] -> Some (formatText title, formatText weekNumber)
       | otherwise -> Some (formatText header.InnerText, "") 

let getBlogEntryFromArticle (article: HtmlNode) : Option<BlogEntry> = 
    match titleAndWeeknumber article with 
    | None -> None 
    | Some (title, weekNumber) -> 
        let categories = getArticleCategories article
        let blogEntry = { Title = title; WeekNumber = weekNumber; Categories = categories }
        Some blogEntry

// for every weekly blog, there is an "article" tag
// get all these tags and make blog entries of them
let extractBlogEntries (rootDoc: HtmlNode) : BlogEntry list =
    rootDoc.QuerySelectorAll("article")
    |> Seq.choose getBlogEntryFromArticle
    |> Seq.toList

// load the main page of f# weekly and extract blog entries with their content
let getBlogEntries() : Async<BlogEntry list> = 
    async { 
        use httpClient = new HttpClient()
        let! content = Async.AwaitTask (httpClient.GetStringAsync("https://sergeytihon.com/category/f-weekly/"))
        let document = HtmlDocument()
        document.LoadHtml(content)
        return extractBlogEntries document.DocumentNode
    }