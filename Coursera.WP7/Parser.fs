module Coursera.Parser

open HtmlAgilityPack.FSharp
open FSharp.Control
open FSharp.Data.Json
open FSharp.Data.Json.Extensions

let parseTopicsJson getLectureSections topicsJsonStr = 

    let parseTopic json =
        { Display = json?display.AsBoolean()         
          Id = json?id.AsInteger()
          Instructor = json?instructor.AsString()
          Language = json?language.AsString()
          LargeIcon = json?large_icon.AsString()
          Name = json?name.AsString()
          Photo = json?photo.AsString()
          PreviewLink = json?preview_link.AsString()
          SelfServiceCourseId = json?self_service_course_id.AsString()
          ShortDescription = json?short_description.AsString()
          ShortName = json?short_name.AsString()
          SmallIcon = json?small_icon.AsString()
          SmallIconHover = json?small_icon_hover.AsString()
          Visibility = json?visibility.AsInteger() }

    let parseCourse topic json =        
        let homeLink = json?home_link.AsString()
        { Id = json?id.AsInteger()
          Name = json?name.AsString()
          StartDate = 
            match json?start_year, json?start_month, json?start_day with
            | JsonValue.Number y, JsonValue.Number m, JsonValue.Number d -> sprintf "%M/%02M/%02M" y m d
            | _ -> ""
          Duration = json?duration_string.AsString()
          HomeLink = homeLink
          HasStarted = json?active.AsBoolean()
          HasFinished = json?status.AsInteger() = 0 || json?certificates_ready.AsBoolean()
          Topic = topic 
          LectureSections = getLectureSections homeLink }

    let courses = 
        [| for topicJson in JsonValue.Parse topicsJsonStr do
            let topic = parseTopic topicJson
            for courseJson in topicJson?courses do
                yield parseCourse topic courseJson |]

    courses
                    
let parseLecturesHtml getHtmlAsync lecturesHtmlStr =

    let trimAndUnescape (text:string) = text.Replace("&nbsp;", "").Trim().Replace("&amp;", "&").Replace("&quot;", "\"").Replace("apos;", "'").Replace("&lt;", "<").Replace("&gt;", ">")
    let endsWith suffix (text:string) = text.EndsWith suffix

    let getVideoUrlAsync iFrameUrl = async {
        let! iframeHtml = iFrameUrl |> getHtmlAsync
        return
            iframeHtml
            |> createDoc
            |> descendants "source" 
            |> Seq.filter (hasAttr "type" "video/mp4")
            |> Seq.head
            |> attr "src" }

    let lectureSections = 
        createDoc lecturesHtmlStr
        |> descendants "h3"
        |> Seq.map (fun h3 ->
            let title = h3 |> innerText |> trimAndUnescape
            let completed = h3 |> parent |> hasClass "course_item_list_header contracted"
            let ul = 
                h3 
                |> parent
                |> followingSibling "ul"
            ul, title, completed)
        |> Seq.filter (fun (ul, _, _) -> ul <> null)
        |> Seq.map (fun (ul, title, completed) -> 
            let lectures =
                ul
                |> elements "li"
                |> Seq.map (element "a")
                |> Seq.map (fun a ->
                    let id = a |> attr "data-lecture-id" |> int
                    let title = innerText a |> trimAndUnescape
                    let videoUrl = a |> attr "data-modal-iframe" 
                                     |> getVideoUrlAsync 
                                     |> LazyAsync.fromAsync
                    let pdfUrl = a |> followingSibling "div" 
                                   |> elements "a" 
                                   |> Seq.map (attr "href") 
                                   |> Seq.tryFind (endsWith ".pdf")
                    let viewed = a |> parent |> hasClass "viewed"
                    { Id = id
                      Title = title
                      VideoUrl = videoUrl
                      PdfUrl = defaultArg pdfUrl ""
                      Viewed = viewed })
                |> Seq.toArray
            { Title = title
              Completed = completed
              Lectures = lectures })
        |> Seq.toArray 

    lectureSections
