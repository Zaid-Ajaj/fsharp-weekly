module Stackoverflow

open Types
open System.IO
open System.IO.Compression
open System.Text
open System.Net.Http
open Newtonsoft.Json
open Newtonsoft.Json.Linq

// stackoverflow responses are compressed using gzip
// here we decompress the response body
let decompressGzip (content: byte[]) =
    use inputStream = new MemoryStream(content)
    use gzipStream = new GZipStream(inputStream, CompressionMode.Decompress)
    use outStream = new MemoryStream()
    gzipStream.CopyTo(outStream)
    outStream.ToArray()

let latestFSharpQuestions() =
    async {
        // url pointing to recent 
        let url = "https://api.stackexchange.com/2.2/questions?order=desc&sort=activity&tagged=fsharp&site=stackoverflow"
        use httpClient = new HttpClient()
        let! response = Async.AwaitTask (httpClient.GetAsync(url))
        let! contentBytes = Async.AwaitTask (response.Content.ReadAsByteArrayAsync())
        let contentDecompressed = decompressGzip contentBytes
        let content = Encoding.UTF8.GetString contentDecompressed
        let contentJson = JObject.Parse(content)
        let items = unbox<JArray> contentJson.["items"]
        let questions = query {
            for item in items do
            select {
                Title = item.["title"].ToObject<string>() |> Scraper.formatText
                Url = item.["link"].ToObject<string>()
                IsAnswered = item.["is_answered"].ToObject<bool>()
                Answers = item.["answer_count"].ToObject<int>()
                Views = item.["view_count"].ToObject<int>()
            }
        }

        return questions
    }