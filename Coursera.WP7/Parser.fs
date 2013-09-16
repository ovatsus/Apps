module Coursera.Parser

open System
open HtmlAgilityPack.FSharp
open FSharp.Control
#if SILVERLIGHT
open FSharp.Data.Json
open FSharp.Data.Json.Extensions
#else
open FSharp.Data
#endif

type ParseError(msg, exn) = 
    inherit Exception(msg, exn)

#if SILVERLIGHT

let parseTopicsJson getLectureSections topicsJsonStr = 

    let topicsJson = JsonValue.Parse topicsJsonStr

    let parseTopic json =
        { Display = json?display.AsBoolean()         
          Id = json?id.AsInteger()
          Instructor = json?instructor.AsString()
          Language = json?language.AsString()
          LargeIcon = json?large_icon.AsString()
          Name = json?name.AsString()
          Photo = json?photo.AsString()
          PreviewLink = json?preview_link.AsString()
          SelfServiceCourseId = 
              let ssId = json?self_service_course_id
              if ssId = JsonValue.Null then None else Some <| ssId.AsInteger()
          ShortDescription = json?short_description.AsString()
          ShortName = json?short_name.AsString()
          SmallIcon = json?small_icon.AsString()
          SmallIconHover = json?small_icon_hover.AsString()
          Visible = json?visibility <> JsonValue.Null }

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
          Active = json?active.AsBoolean()
          HasFinished = json?status.AsInteger() = 0 || json?certificates_ready.AsBoolean()
          Topic = topic 
          LectureSections = getLectureSections homeLink }

    let courses = 
        try 
            [| for topicJson in topicsJson do
                let topic = parseTopic topicJson
                if topic.Visible && topic.Display then
                    for courseJson in topicJson?courses do
                        yield parseCourse topic courseJson |]
        with exn ->
            raise <| ParseError(sprintf "Failed to parse topics JSON:\n%s\n" topicsJsonStr, exn)

    courses

#else

type JsonT = JsonProvider<"topics.json", SampleList=true, RootName="topic">

let parseTopicsJson getLectureSections topicsJsonStr = 

    let topicsJson = JsonT.Parse topicsJsonStr

    let parseTopic (json:JsonT.DomainTypes.Topic) =
        { Display = json.Display
          Id = json.Id
          Instructor = json.Instructor
          Language = json.Language
          LargeIcon = json.LargeIcon
          Name = json.Name
          Photo = json.Photo
          PreviewLink = json.PreviewLink
          SelfServiceCourseId = json.SelfServiceCourseId.Number
          ShortDescription = json.ShortDescription
          ShortName = json.ShortName
          SmallIcon = json.SmallIcon
          SmallIconHover = json.SmallIcon
          Visible = json.Visibility.Number.IsSome }

    let parseCourse topic (json:JsonT.DomainTypes.Course) =
        let homeLink = json.HomeLink
        { Id = json.Id
          Name = json.Name.String.Value
          StartDate = 
            match json.StartYear.Number, json.StartMonth.Number, json.StartDay.Number with
            | Some y, Some m, Some d -> sprintf "%d/%02d/%02d" y m d
            | _ -> ""
          Duration = json.DurationString
          HomeLink = homeLink
          Active = json.Active
          HasFinished = json.CertificatesReady || not json.Status
          Topic = topic 
          LectureSections = getLectureSections homeLink }

    let courses = 
        try 
            [| for topicJson in topicsJson do
                let topic = parseTopic topicJson
                if topic.Visible && topic.Display then
                    for courseJson in topicJson.Courses do
                        yield parseCourse topic courseJson |]
        with exn ->
            raise <| ParseError(sprintf "Failed to parse topics JSON:\n%s\n" topicsJsonStr, exn)

    courses

#endif

let parseLecturesHtml getHtmlAsync lecturesHtmlStr =

    let trimAndUnescape (text:string) = text.Replace("&nbsp;", "").Trim().Replace("&amp;", "&").Replace("&quot;", "\"").Replace("apos;", "'").Replace("&lt;", "<").Replace("&gt;", ">")
    let endsWith suffix (text:string) = text.EndsWith suffix

    let getVideoUrlAsync iFrameUrl = async {
        let! iframeHtml = iFrameUrl |> getHtmlAsync
        try 
            return
                iframeHtml
                |> createDoc
                |> descendants "source" 
                |> Seq.filter (hasAttr "type" "video/mp4")
                //TODO: this crashes on some courses
                |> Seq.head
                |> attr "src"
        with exn ->
            raise <| ParseError(sprintf "Failed to parse video HTML:\n%s\n" iframeHtml, exn)
            return ""
    }

    let lectureSections = 
        try
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
                        let lectureNotesUrl = 
                            let urls = a |> followingSibling "div" 
                                         |> elements "a" 
                                         |> Seq.map (attr "href") 
                            match Seq.tryFind (endsWith ".ppsx") urls with
                            | Some url -> url
                            | _ -> match Seq.tryFind (endsWith ".pps") urls with
                                   | Some url -> url
                                   | _ -> match Seq.tryFind (endsWith ".pptx") urls with
                                          | Some url -> url
                                          | _ -> match Seq.tryFind (endsWith ".ppt") urls with
                                                 | Some url -> url
                                                 | _ -> match Seq.tryFind (endsWith ".pdf") urls with
                                                        | Some url -> url
                                                        | _ -> ""
                        let viewed = a |> parent |> hasClass "viewed"
                        { Id = id
                          Title = title
                          VideoUrl = videoUrl
                          LectureNotesUrl = lectureNotesUrl
                          Viewed = viewed })
                    |> Seq.toArray
                { Title = title
                  Completed = completed
                  Lectures = lectures })
            |> Seq.toArray 
        with exn ->
            raise <| ParseError(sprintf "Failed to parse lectures HTML:\n%s\n" lecturesHtmlStr, exn)

    lectureSections
