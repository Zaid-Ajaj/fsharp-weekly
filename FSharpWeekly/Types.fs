module Types

type Link = {
    Url : string
    Content : string
}

type Category = {
    Name: string
    Links: Link list
}

type BlogEntry = {
    Title: string
    WeekNumber: string
    Categories: Category list
}

type Remote<'t> =
    | Empty
    | Loading
    | LoadError of string
    | Content of 't

type State = {
    Blogs: Remote<BlogEntry list>
    CurrentBlog : Option<BlogEntry>
    ShowingSettings : bool
    VisitedLinks : string list
    LinkTrackingEnabled : bool
}

type Msg =
    | LoadBlogs
    | BlogsLoaded of BlogEntry list
    | BlogsLoadFailure of string
    | SelectBlog of BlogEntry
    | OpenUrl of string
    | PoppedPage of string
    | ShowSettings
    | ToggleLinkTracking
    | ClearVisitedLinks