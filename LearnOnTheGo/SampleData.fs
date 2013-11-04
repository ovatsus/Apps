namespace LearnOnTheGo

open System.ComponentModel
open System.IO
open System.Reflection
open FSharp.Control

// an object expression with just the interface implementation won't work with data binding
type DownloadInfo (courseId, courseTopicName, lectureId, lectureTitle, downloaded, index) =    
    let propertyChanged = Event<_,_>()
    member __.CourseId = courseId
    member __.CourseTopicName = courseTopicName
    member __.LectureId = lectureId
    member __.LectureTitle = lectureTitle
    member __.Index = index
    member __.Downloading = false
    member __.Downloaded = downloaded
    member __.VideoLocation = null 
    interface IDownloadInfo with
        member x.CourseId = x.CourseId
        member x.CourseTopicName = x.CourseTopicName
        member x.LectureId = x.LectureId
        member x.LectureTitle = x.LectureTitle
        member x.Index = x.Index
        member x.Downloading = x.Downloading
        member x.Downloaded = x.Downloaded
        member x.VideoLocation = x.VideoLocation
        member x.RefreshStatus() = ()
        member x.QueueDowload(_) = ()
        member x.DeleteVideo() = ()
    interface INotifyPropertyChanged with
        [<CLIEvent>]
        member __.PropertyChanged = propertyChanged.Publish

type SampleData() =

    let getLectures _ _ _ = LazyAsync.fromValue [| |]

    let activeCourses, upcomingCourses, finishedCourses = 
        use stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("topics.json")
        use streamReader = new StreamReader(stream)
        streamReader.ReadLine() |> Parser.parseTopicsJson getLectures |> Array.rev, 
        streamReader.ReadLine() |> Parser.parseTopicsJson getLectures |> Array.rev, 
        streamReader.ReadLine() |> Parser.parseTopicsJson getLectures |> Array.rev
    
    let getHtmlAsync _ = async { return "" }

    static let mutable downloaded = false

    let createDownloadInfo courseId courseTopicName lectureId lectureTitle index =
        downloaded <- not downloaded
        DownloadInfo(courseId, courseTopicName, lectureId, lectureTitle, downloaded, index) :> IDownloadInfo

    let lectureSections =  
        use stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("lectures.html")
        use streamReader = new StreamReader(stream)
        streamReader.ReadToEnd() 
        |> Parser.parseLecturesHtml getHtmlAsync (createDownloadInfo activeCourses.[0].Id activeCourses.[0].Topic.Name)
        |> Seq.skip 2
        |> Seq.toArray

    let downloadsInProgress = lectureSections.[0].Lectures |> Seq.map (fun lecture -> lecture.DownloadInfo) |> Seq.toArray
    let completedDownloads = lectureSections.[1].Lectures |> Seq.map (fun lecture -> lecture.DownloadInfo) |> Seq.toArray

    member __.ActiveCourses = activeCourses
    member __.UpcomingCourses = upcomingCourses
    member __.FinishedCourses = finishedCourses

    member __.CourseTitle = activeCourses.[0].Topic.Name
    member __.LectureSections = lectureSections

    member __.DownloadsInProgress = downloadsInProgress
    member __.CompletedDownloads = completedDownloads
