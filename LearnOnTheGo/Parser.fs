module LearnOnTheGo.Parser

open System
open System.Net
open FSharp.Control
open FSharp.Data

type ParseError(msg, exn) = 
    inherit Exception(msg, exn)

type JsonT = JsonProvider<"topics.json", SampleIsList=true, RootName="topic">
type JsonT2 = JsonProvider<"topics2.json">

let parseTopicsJson getLectureSections topicsJsonStr = 

    try 

        let topicsJson = JsonT.Parse topicsJsonStr
    
        let parseTopic (json:JsonT.Topic) =
            { Display = json.Display
              Id = json.Id
              Instructor = defaultArg json.Instructor ""
              Language = json.Language
              LargeIcon = json.LargeIcon
              Name = json.Name
              Photo = json.Photo
              PreviewLink = json.PreviewLink
              SelfServiceCourseId = json.SelfServiceCourseId
              ShortDescription = json.ShortDescription
              ShortName = json.ShortName
              SmallIcon = json.SmallIcon
              SmallIconHover = json.SmallIcon }
    
        let parseCourse topic (json:JsonT.Course) =
            let id = json.Id
            let homeLink = defaultArg json.HomeLink ""
            { Id = id
              Name = json.Name.String.Value
              StartDate = 
                match json.StartYear, json.StartMonth, json.StartDay with
                | Some y, Some m, Some d -> sprintf "%d/%02d/%02d" y m d
                | _ -> ""
              Duration = json.DurationString
              HomeLink = homeLink
              Active = json.Active
              HasFinished = json.GradesReleaseDate.IsSome || json.CertificatesReady
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

    let getVideoUrlAsync iFrameUrl = async {
        let! iframeHtml = iFrameUrl |> getHtmlAsync
        try 
            return
                iframeHtml
                |> HtmlDocument.Parse
                |> HtmlDocument.descendantsNamed false ["source"]
                |> Seq.filter (HtmlNode.hasAttribute "type" "video/mp4")
                //TODO: this crashes in  970962 = Roman Architecture [001], Lecture = 5.3 Hellenized Houses in Pompeii - 13:27 [51]
                |> Seq.head
                |> HtmlNode.attributeValue "src"
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
            lecturesHtmlStr
            |> HtmlDocument.Parse
            |> HtmlDocument.descendantsNamedWithPath false ["h3"]
            |> Seq.choose (fun (h3, parents) ->
                let title = Html.innerText h3
                let completed = parents |> List.head |> HtmlNode.hasClass "course-item-list-header contracted"
                let ul = 
                    (List.head parents, List.tail parents)
                    |> Html.followingSibling "ul"
                ul |> Option.map (fun ul -> ul, title, completed))
            |> Seq.map (fun (ul, title, completed) -> 
                let lectures =
                    ul
                    |> HtmlNode.elementsNamed ["li"]
                    |> List.map (fun li -> li, li |> HtmlNode.elementsNamed ["a"] |> List.head)
                    |> List.map (fun (li, a) ->
                        let id = a |> HtmlNode.attributeValue "data-lecture-id" |> int
                        let title = Html.innerText a
                        let quizAttemptedSpan = a |> HtmlNode.elementsNamed ["span"] |> List.tryFind (HtmlNode.hasClass "label label-success")
                        let title, quizAttempted =
                            match quizAttemptedSpan with
                            | Some span -> title |> String.remove (Html.innerText a) |> String.trim, true
                            | None -> title, false
                        let videoUrl = a |> HtmlNode.attributeValue "data-modal-iframe" 
                                         |> getVideoUrlAsync 
                                         |> LazyAsync.fromAsync
                        let lectureNotesUrl = 
                            let urls = li 
                                       |> HtmlNode.elementsNamed ["div"]
                                       |> Seq.exactlyOne
                                       |> HtmlNode.elementsNamed ["a" ]
                                       |> List.map (HtmlNode.attributeValue "href") 
                            [".ppsx"; ".pps"; ".pptx"; ".ppt"; ".pdf"] 
                            |> List.tryPick (fun ext -> List.tryFind (String.endsWith ext) urls)
                        let viewed = li |> HtmlNode.hasClass "viewed"
                        incr index
                        { Id = id
                          Title = title
                          VideoUrl = videoUrl
                          LectureNotesUrl = defaultArg lectureNotesUrl ""
                          Viewed = viewed 
                          QuizAttempted = quizAttempted
                          DownloadInfo = createDownloadInfo id title !index })
                    |> List.toArray
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
