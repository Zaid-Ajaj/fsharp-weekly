// Copyright 2018 Fabulous contributors. See LICENSE.md for license.
namespace FSharpWeekly

open Fabulous
open Fabulous.XamarinForms
open Xamarin.Forms
open System
open Types

module Pages =
    let [<Literal>] blog = "blog-content"
    let [<Literal>] settings = "settings"

module App =

    let initModel = {
        Blogs = Remote.Empty;
        TrendyGithubRepositories = Remote.Empty
        StackoverflowQuestions = Remote.Empty
        CurrentBlog = None
        VisitedLinks = [ ]
        ShowingSettings = false
        LinkTrackingEnabled = true
    }

    let init () = initModel, Cmd.ofMsg LoadBlogs

    let extractBlogEntries() =
        Cmd.ofAsyncMsg (async {
            let! blogEntries = Async.Catch (Scraper.getBlogEntries())
            match blogEntries with
            | Choice1Of2 blogs -> return BlogsLoaded blogs
            | Choice2Of2 error -> return BlogsLoadFailure "Error while loading blog entries from F# weekly"
        })

    let extractTrendyRepos() =
        Cmd.ofAsyncMsg (async {
            let! trendyRepos = Async.Catch(GithubScraper.trendyToday())
            match trendyRepos with
            | Choice1Of2 repos -> return ReposLoaded repos
            | Choice2Of2 ex -> return ReposLoadFailure ex.Message
        })

    let getStackoverflowQuestions() =
        Cmd.ofAsyncMsg(async {
            let! questions = Async.Catch(Stackoverflow.latestFSharpQuestions())
            match questions with
            | Choice1Of2 questions -> return QuestionsLoaded (List.ofSeq questions)
            | Choice2Of2 error -> return QuestionsLoadFailure error.Message
        })

    let update msg state =
        match msg with
        | LoadBlogs ->
            let nextState = { state with Blogs = Loading; CurrentBlog = None }
            nextState, extractBlogEntries()

        | BlogsLoaded blogs ->
            let nextState = { state with Blogs = Content blogs }
            nextState, Cmd.none

        | BlogsLoadFailure error ->
            let nextState = { state with Blogs = LoadError error }
            nextState, Cmd.none

        | LoadQuestions ->
            let nextState = { state with StackoverflowQuestions = Loading }
            nextState, getStackoverflowQuestions()

        | QuestionsLoaded questions ->
            let nextState = { state with StackoverflowQuestions = Content questions }
            nextState, Cmd.none

        | QuestionsLoadFailure error ->
            let nextState = { state with StackoverflowQuestions = LoadError error }
            nextState, Cmd.none

        | PageIndexChanged pageIndex ->
            match pageIndex with
            | 0 -> state, Cmd.ofMsg LoadBlogs
            | 1 -> state, Cmd.ofMsg LoadRepos
            | 2 -> state, Cmd.ofMsg LoadQuestions
            | n -> state, Cmd.none

        | LoadRepos ->
            let nextState = { state with TrendyGithubRepositories = Loading }
            nextState, extractTrendyRepos()

        | ReposLoaded repos ->
            let nextState = { state with TrendyGithubRepositories = Content repos }
            nextState, Cmd.none

        | ReposLoadFailure error ->
            let nextState = { state with TrendyGithubRepositories = LoadError error }
            nextState, Cmd.none

        | SelectBlog blogEntry ->
            let nextState = { state with CurrentBlog = Some blogEntry; VisitedLinks = List.append state.VisitedLinks [blogEntry.WeekNumber] }
            nextState, Cmd.none

        | ShowSettings ->
            let nextState = { state with ShowingSettings = true }
            nextState, Cmd.none

        | PoppedPage Pages.settings ->
            let nextState = { state with ShowingSettings = false }
            nextState, Cmd.none

        | PoppedPage Pages.blog ->
            let nextState = { state with CurrentBlog = None }
            nextState, Cmd.none

        | PoppedPage other ->
            state, Cmd.none

        | ClearVisitedLinks ->
            let nextState = { state with VisitedLinks = List.empty }
            nextState, Cmd.none

        | ToggleLinkTracking ->
            let nextState = { state with LinkTrackingEnabled = not state.LinkTrackingEnabled }
            nextState, Cmd.none

        | OpenUrl link ->
            let nextState = { state with VisitedLinks = List.append state.VisitedLinks [link] }
            // custom side-effect, opens the url in default browser
            nextState, [ fun dispatch -> Device.OpenUri(Uri(link)) ]

    let loader =
        ActivityIndicator.activityIndicator [
            ActivityIndicator.Color Color.LightBlue
            ActivityIndicator.IsRunning true
        ]

    let fsharpIconSource =
        match Device.PlatformServices.RuntimePlatform with
        | Device.UWP -> ImageSource.FromFile("Assets/fsharp-icon.png")
        | otherwise -> ImageSource.FromUri(Uri("https://fsharp.org/img/logo/fsharp256.png"))

    let render (state: State) dispatch =
        // gesture recognizers
        let whenClicked msg = View.ClickGestureRecognizer(command = fun () -> dispatch msg)
        let whenTapped msg = View.TapGestureRecognizer(command = fun () -> dispatch msg)

        let settingsButton =
            Image.image [
               Image.Source (ImageSource.FromUri(Uri("https://cdn4.iconfinder.com/data/icons/wirecons-free-vector-icons/32/menu-alt-512.png")))
               Image.HorizontalLayout LayoutOptions.EndAndExpand
               Image.VerticalLayout LayoutOptions.Fill
               Image.BackgroundColor Color.Transparent
               Image.GestureRecognizers [ whenClicked ShowSettings; whenTapped ShowSettings ]
               Image.Height 40.0
               Image.Width 40.0
               Image.Opacity 0.60
            ]
            
        let headerNamed name showSettings =
             StackLayout.stackLayout [
                 StackLayout.Orientation StackOrientation.Horizontal
                 StackLayout.VerticalLayout LayoutOptions.Start
                 StackLayout.Padding 20.0
                 StackLayout.Children [
                     yield Image.image [
                         Image.Source fsharpIconSource
                         Image.Height 60.0
                         Image.Width 60.0
                     ]

                     yield Label.label [
                         Label.Text name
                         Label.FontSize FontSize.Large
                         Label.Margin 10.0
                     ]

                     if showSettings then yield settingsButton
                 ]
             ]

        let blogsHeaders = headerNamed "FSharp Weekly" true

        let blogVisited (blog: BlogEntry) =
            state.VisitedLinks
            |> List.exists (fun url -> url = blog.WeekNumber)

        let blogLabelColor (blog: BlogEntry) =
            if blogVisited blog && state.LinkTrackingEnabled
            then Color.DarkGray
            else Color.Black

        // renders a single blog item on the main page 
        let renderBlogItem (blog: BlogEntry) =
            StackLayout.stackLayout [
                StackLayout.Orientation StackOrientation.Vertical
                StackLayout.Padding 20.0

                StackLayout.GestureRecognizers [
                    whenClicked (SelectBlog blog)
                    whenTapped (SelectBlog blog)
                ]

                StackLayout.Children [
                    // blog title
                    Label.label [
                        Label.Text blog.Title
                        Label.FontSize FontSize.Large
                        Label.TextColor (blogLabelColor blog)
                    ]
                    // subtitle: blog week number
                    Label.label [
                        Label.Text blog.WeekNumber
                        Label.FontSize FontSize.Small
                        Label.TextColor (blogLabelColor blog)
                    ]
                ]
            ]

        // renders a single blog item on the main page 
        let renderRepoItem (repo: GithubRepository) =
            StackLayout.stackLayout [
                StackLayout.Orientation StackOrientation.Vertical
                StackLayout.Padding 20.0

                StackLayout.Children [
                    // blog title
                    
                    Label.label [
                        Label.Text (sprintf "%s / %s" repo.Owner repo.Name)
                        Label.FontSize FontSize.Large
                        Label.TextColor Color.Black
                        Label.TextDecorations TextDecorations.Underline
                        Label.GestureRecognizers [
                            whenTapped (OpenUrl repo.Url)
                            whenClicked (OpenUrl repo.Url)
                        ]
                    ]
                    // repo description
                    Label.label [
                        Label.Text repo.Description
                        Label.FontSize FontSize.Small
                        Label.TextColor Color.Black
                    ]
                    // stars
                    Label.label [
                        Label.Text (sprintf "☆ %d" repo.StarCount)
                        Label.FontSize FontSize.Medium
                    ]
                ]
            ]
        // renders a list of blog entries 
        let renderBlogs (entries: BlogEntry list) =
            StackLayout.stackLayout [
                StackLayout.Orientation StackOrientation.Vertical
                StackLayout.Children [ for blog in entries -> renderBlogItem blog ]
            ]

        // the content/body of the current page
        let content =
            match state.Blogs with
            | Empty -> View.BoxView()
            | Loading -> loader 
            | LoadError errorMsg ->
                Label.label [
                    Label.Text errorMsg
                    Label.Margin 20.0
                    Label.TextColor Color.Red
                    Label.HorizontalTextAlignment TextAlignment.Center
                ]
            | Content blogItems -> renderBlogs blogItems

        // no blog selected, load all blogs
        let mainPage =
            StackLayout.stackLayout [
               StackLayout.Orientation StackOrientation.Vertical
               StackLayout.GestureRecognizers [
                   // swipe from header to reload blog entries
                   View.SwipeGestureRecognizer(
                        direction=SwipeDirection.Down,
                        swiped = fun args -> dispatch LoadBlogs)
               ]

               StackLayout.Children [
                   // header is constant
                   blogsHeaders
                   // the content of blogs in scrollable
                   ScrollView.scrollView [ ScrollView.Content content ]
               ]
            ]

        let linkVisited (link: Link) =
            state.VisitedLinks
            |> List.exists (fun url -> url = link.Url)

        let linkColor (link: Link) =
            if linkVisited link && state.LinkTrackingEnabled
            then Color.DarkGray
            else Color.Black

        let renderBlogContent (blog: BlogEntry) = 
            let renderLink (link: Link) =
                Label.label [
                    Label.Text link.Content
                    Label.FontSize FontSize.Medium
                    Label.TextColor (linkColor link)
                    Label.Margin 5.0
                    Label.TextDecorations TextDecorations.Underline
                    Label.GestureRecognizers [
                        whenTapped (OpenUrl link.Url)
                        whenClicked (OpenUrl link.Url)
                    ]
                ]
            
            let renderCategory (category: Category) =
                StackLayout.stackLayout [
                    StackLayout.Orientation StackOrientation.Vertical
                    StackLayout.PaddingTop 20.0
                    StackLayout.PaddingBottom 20.0
                    StackLayout.Children [
                        yield Label.label [ Label.Text category.Name; Label.FontSize FontSize.Large; Label.FontAttributes FontAttributes.Bold ]
                        yield! [ for link in category.Links -> renderLink link ]
                    ]
                ]

            let blogContent = StackLayout.stackLayout [
                StackLayout.Orientation StackOrientation.Vertical
                StackLayout.Padding 20.0
                StackLayout.Children [
                    yield Label.label [ Label.Text blog.Title; Label.FontSize FontSize.Large; ]
                    yield! [ for category in blog.Categories -> renderCategory category ]
                ]
            ]

            ScrollView.scrollView [ ScrollView.Content blogContent ]

        let settingsPage =
            let layout =
                let header = 
                    StackLayout.stackLayout [
                        StackLayout.Orientation StackOrientation.Horizontal
                        StackLayout.Padding 20.0
                        StackLayout.Children [
                             Image.image [
                                 Image.Source fsharpIconSource
                                 Image.Height 60.0
                                 Image.Width 60.0
                             ]

                             Label.label [
                                 Label.Text "Settings"
                                 Label.FontSize FontSize.Large
                                 Label.Margin 10.0
                             ]
                        ]
                    ]

                StackLayout.stackLayout [
                    StackLayout.Orientation StackOrientation.Vertical
                    StackLayout.Children [
                        header

                        StackLayout.stackLayout [
                            StackLayout.Orientation StackOrientation.Horizontal
                            StackLayout.Padding 20.0
                            StackLayout.Children [
                                Label.label [
                                    Label.Text "Enable Link Tracking"
                                    Label.FontSize FontSize.Large
                                ]

                                Switch.switch [
                                    Switch.HorizontalLayout LayoutOptions.EndAndExpand
                                    Switch.VerticalLayout LayoutOptions.Fill
                                    Switch.IsToggled state.LinkTrackingEnabled
                                    Switch.Scale 1.5
                                    Switch.OnToggled (fun args -> dispatch ToggleLinkTracking)
                                ]
                            ]
                        ]

                        Button.button [
                            Button.Text "Clear Visited Links"
                            Button.BackgroundColor Color.LightBlue
                            Button.TextColor Color.White
                            Button.CornerRadius 5
                            Button.Margin 20.0
                            Button.OnClick (fun _ -> dispatch ClearVisitedLinks)
                        ]

                        StackLayout.stackLayout [
                            StackLayout.Orientation StackOrientation.Horizontal
                            StackLayout.HorizontalLayout LayoutOptions.Center
                            StackLayout.VerticalLayout LayoutOptions.EndAndExpand
                            StackLayout.Padding 10.0
                            StackLayout.Children [
                                Label.label [ Label.Text "Made with ❤ by"; Label.FontSize FontSize.Medium ]
                                Label.label [
                                    Label.Text "Zaid Ajaj"
                                    Label.FontSize FontSize.Medium
                                    Label.TextDecorations TextDecorations.Underline
                                    Label.GestureRecognizers [
                                        whenClicked (OpenUrl "https://github.com/Zaid-Ajaj")
                                        whenTapped (OpenUrl "https://github.com/Zaid-Ajaj")
                                    ]
                                ]
                            ]
                        ]

                        Label.label [
                            Label.Text "Source code available on Github"
                            Label.FontSize FontSize.Medium
                            Label.MarginBottom 20.0
                            Label.HorizontalLayout LayoutOptions.Center
                            Label.TextDecorations TextDecorations.Underline
                            Label.GestureRecognizers [
                                whenClicked (OpenUrl "https://github.com/Zaid-Ajaj/fsharp-weekly")
                                whenTapped (OpenUrl "https://github.com/Zaid-Ajaj/fsharp-weekly")
                            ]
                        ]
                    ]
                ]

            ScrollView.scrollView [ ScrollView.Content layout ]

        let fsharpBlogsPage = 
            NavigationPage.navigationPage [
                NavigationPage.Title "F# Blogs"
                NavigationPage.Icon "Appicon.png"
                NavigationPage.OnPopped (fun args -> dispatch (PoppedPage args.Page.ClassId))
                NavigationPage.Pages [
                    // render the root page
                    yield ContentPage.contentPage [
                        ContentPage.ClassId "main"
                        ContentPage.HasNavigationBar false
                        ContentPage.Content mainPage
                    ]

                    // if a blog entry is selected -> push it to the page stack
                    match state.CurrentBlog with
                    | None -> ()
                    | Some blog -> yield ContentPage.contentPage [
                        // remove back button and navigation bar if running on android
                        ContentPage.HasBackButton (Device.RuntimePlatform <> Device.Android)
                        ContentPage.HasNavigationBar (Device.RuntimePlatform <> Device.Android)
                        ContentPage.ClassId Pages.blog
                        ContentPage.Title blog.WeekNumber
                        ContentPage.Content (renderBlogContent blog)
                    ]

                    // if we clicked on the settings button,
                    // add the settings page to the page stack
                    if state.ShowingSettings
                    then yield ContentPage.contentPage [
                        ContentPage.ClassId Pages.settings
                        ContentPage.HasBackButton (Device.RuntimePlatform <> Device.Android)
                        ContentPage.HasNavigationBar (Device.RuntimePlatform <> Device.Android)
                        ContentPage.Title "Settings"
                        ContentPage.Content settingsPage
                    ]
                ]    
            ]

        let trendyReposPage =

            let scrollableContent =
                match state.TrendyGithubRepositories with
                | Empty ->
                    View.BoxView()
                | Loading ->
                    loader
                | LoadError error ->
                    Label.label [ Label.Text ("Error while loading repos: " + error) ]
                | Content repos ->
                    StackLayout.stackLayout [
                        StackLayout.Children [ for repo in repos -> renderRepoItem repo ]
                    ]

            let repositories =
                StackLayout.stackLayout [

                    StackLayout.Orientation StackOrientation.Vertical
                    StackLayout.GestureRecognizers [
                       // swipe from header to reload blog entries
                       View.SwipeGestureRecognizer(
                            direction=SwipeDirection.Down,
                            swiped = fun args -> dispatch LoadRepos)
                    ]

                    StackLayout.Children [
                        headerNamed "Trending F# Repositories" false

                        ScrollView.scrollView [
                            ScrollView.Content scrollableContent
                        ]
                    ]
                ]
            
            ContentPage.contentPage [
                ContentPage.Title "Github"
                ContentPage.Icon "github.png"
                ContentPage.Content repositories
            ]

        let stackoverflowPage =

            let questionItem (question: StackoverflowQuestion) =
                let answersText =
                    match question.Answers with
                    | 1 -> "1 answer"
                    | n -> sprintf "%d answers" n

                let viewsText =
                    match question.Views with
                    | 1 -> "1 view"
                    | n -> sprintf "%d views" n

                StackLayout.stackLayout [
                    StackLayout.Padding 20.0
                    StackLayout.GestureRecognizers [
                        whenClicked (OpenUrl question.Url)
                        whenTapped (OpenUrl question.Url)
                    ]

                    StackLayout.Children [
                        Label.label [
                           Label.FontSize FontSize.Large
                           Label.Text question.Title
                           Label.TextColor Color.Black
                        ]

                        StackLayout.stackLayout [
                            StackLayout.Orientation StackOrientation.Horizontal
                            StackLayout.Children [
                                Label.label [
                                   Label.FontSize FontSize.Small
                                   Label.Text answersText
                                   Label.TextColor (if question.IsAnswered then Color.LightGreen else Color.Gray)
                                ]

                                Label.label [
                                   Label.MarginLeft 10.0
                                   Label.FontSize FontSize.Small
                                   Label.Text viewsText
                                   Label.TextColor Color.Gray
                                ]
                            ]
                        ]
                    ]
                ]

            let questions =
                StackLayout.stackLayout [
                    StackLayout.Children [
                        match state.StackoverflowQuestions with
                        | Empty ->  yield View.BoxView()
                        | Loading -> yield loader 
                        | LoadError errorMsg -> yield Label.label [ Label.Text errorMsg ]
                        | Content questions -> for question in questions do yield questionItem question
                    ]
                ]

            let mainLayout =
                StackLayout.stackLayout [
                    StackLayout.Orientation StackOrientation.Vertical
                    StackLayout.GestureRecognizers [
                       // swipe from header to reload blog entries
                       View.SwipeGestureRecognizer(
                            direction=SwipeDirection.Down,
                            swiped = fun args -> dispatch LoadQuestions)
                    ]
                    StackLayout.Children [
                        headerNamed "F# Stackoverflow Questions" false
                        ScrollView.scrollView [
                            ScrollView.Content questions
                        ]
                    ]
                ]

            ContentPage.contentPage [
                ContentPage.Title "Stackoverflow"
                ContentPage.Icon "so.png"
                ContentPage.Content mainLayout
            ]
            
        TabbedPage.tabbedPage [
            TabbedPage.OnCurrentPageChanged (function
                | None -> ()
                | Some pageIndex -> dispatch (PageIndexChanged pageIndex)
            )

            TabbedPage.Children [
                fsharpBlogsPage
                trendyReposPage
                stackoverflowPage
            ]
        ]

type PersistedAppConfig = {
    LinkTrackingEnabled: bool
    VisitedLinks: string list
}

type App () as app = 
    inherit Application ()

    let program = Program.mkProgram App.init App.update App.render

    let runner = 
        program
#if DEBUG
        |> Program.withConsoleTrace
#endif
        |> XamarinFormsProgram.run app

#if DEBUG
    // Uncomment this line to enable live update in debug mode. 
    // See https://fsprojects.github.io/Fabulous/tools.html for further  instructions.
    //
    //do runner.EnableLiveUpdate()
#endif    

    
    let configKey = "config"
    override __.OnSleep() = 
        let currentConfig : PersistedAppConfig = {
            LinkTrackingEnabled = runner.CurrentModel.LinkTrackingEnabled
            VisitedLinks = runner.CurrentModel.VisitedLinks
        }

        let visitedLinksJson = Newtonsoft.Json.JsonConvert.SerializeObject(currentConfig)
        app.Properties.[configKey] <- visitedLinksJson

    override __.OnResume() = 
        try 
            match app.Properties.TryGetValue configKey with
            | true, (:? string as json) -> 
                let config = Newtonsoft.Json.JsonConvert.DeserializeObject<PersistedAppConfig>(json)
                let nextState = { runner.CurrentModel with VisitedLinks = config.VisitedLinks; LinkTrackingEnabled = config.LinkTrackingEnabled }
                runner.SetCurrentModel (nextState, Cmd.none)
            | _ ->
                ()
        with ex -> 
            ignore()

    override this.OnStart() = 
        this.OnResume()