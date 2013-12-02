module LearnOnTheGo.Parser

open System
open System.Net
open HtmlAgilityPack.FSharp
open FSharp.Control
open FSharp.Data

type ParseError(msg, exn) = 
    inherit Exception(msg, exn)

type JsonT = JsonProvider<"topics.json", SampleIsList=true, RootName="topic">

let parseTopicsJson getLectureSections topicsJsonStr = 

    try 

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
              SmallIconHover = json.SmallIcon }
    
        let parseCourse topic (json:JsonT.DomainTypes.Course) =
            let id = json.Id
            let homeLink = json.HomeLink
            { Id = id
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
              LectureSections = getLectureSections id topic.Name homeLink }
    
        [| for topicJson in topicsJson do
            let topic = parseTopic topicJson
            if topic.Display then
                for courseJson in topicJson.Courses do
                    yield parseCourse topic courseJson |]

    with exn ->
        if topicsJsonStr.IndexOf("Wi-Fi", StringComparison.OrdinalIgnoreCase) > 0 ||
           topicsJsonStr.IndexOf("WiFi", StringComparison.OrdinalIgnoreCase) > 0 then
            raise <| new WebException()
        else
            raise <| ParseError(sprintf "Failed to parse topics JSON:\n%s\n" topicsJsonStr, exn)

let parseLecturesHtml getHtmlAsync createDownloadInfo lecturesHtmlStr =

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
            if iframeHtml.IndexOf("Wi-Fi", StringComparison.OrdinalIgnoreCase) > 0 ||
               iframeHtml.IndexOf("WiFi", StringComparison.OrdinalIgnoreCase) > 0 then
                raise <| new WebException()
            else
                raise <| ParseError(sprintf "Failed to parse video HTML:\n%s\n" iframeHtml, exn)
            return ""
    }

    let lectureSections = 
        let index = ref -1
        try
            createDoc lecturesHtmlStr
            |> descendants "h3"
            |> Seq.map (fun h3 ->
                let title = h3 |> innerText |> trimAndUnescape
                let completed = h3 |> parent |> hasClass "course-item-list-header contracted"
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
                        let quizAttemptedSpan = a |> elements "span" |> Seq.tryFind (hasClass "label label-success")
                        let title, quizAttempted =
                            match quizAttemptedSpan with
                            | Some span ->
                                title.Replace(trimAndUnescape span.InnerText, "").Trim(), true
                            | None -> title, false
                        let videoUrl = a |> attr "data-modal-iframe" 
                                         |> getVideoUrlAsync 
                                         |> LazyAsync.fromAsync
                        let lectureNotesUrl = 
                            let urls = a |> followingSibling "div" 
                                         |> elements "a" 
                                         |> Seq.map (attr "href") 
                                         |> Seq.toList
                            [".ppsx"; ".pps"; ".pptx"; ".ppt"; ".pdf"] 
                            |> List.tryPick (fun ext -> List.tryFind (endsWith ext) urls)
                        let viewed = a |> parent |> hasClass "viewed"
                        incr index
                        { Id = id
                          Title = title
                          VideoUrl = videoUrl
                          LectureNotesUrl = defaultArg lectureNotesUrl ""
                          Viewed = viewed 
                          QuizAttempted = quizAttempted
                          DownloadInfo = createDownloadInfo id title !index })
                    |> Seq.toArray
                { Title = title
                  Completed = completed
                  Lectures = lectures })
            |> Seq.toArray 
        with exn ->
            if lecturesHtmlStr.IndexOf("Wi-Fi", StringComparison.OrdinalIgnoreCase) > 0 ||
               lecturesHtmlStr.IndexOf("WiFi", StringComparison.OrdinalIgnoreCase) > 0 then
                raise <| new WebException()
            else
                raise <| ParseError(sprintf "Failed to parse lectures HTML:\n%s\n" lecturesHtmlStr, exn)

    if lectureSections.Length = 0 &&
       (lecturesHtmlStr.IndexOf("Wi-Fi", StringComparison.OrdinalIgnoreCase) > 0 ||
        lecturesHtmlStr.IndexOf("WiFi", StringComparison.OrdinalIgnoreCase) > 0) then
        raise <| new WebException()

    lectureSections
