namespace Coursera

open System.IO
open System.Reflection
open FSharp.Control

type SampleData() =

    let getLectures _ = LazyAsync.fromValue [| |]

    let activeCourses, upcomingCourses, finishedCourses = 
        use stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("topics.json")
        use streamReader = new StreamReader(stream)
        streamReader.ReadLine() |> Parser.parseTopicsJson getLectures |> Array.rev, 
        streamReader.ReadLine() |> Parser.parseTopicsJson getLectures |> Array.rev, 
        streamReader.ReadLine() |> Parser.parseTopicsJson getLectures |> Array.rev
    
    let getHtmlAsync _ = async { return "" }

    let lectureSections =  
        use stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("lectures.html")
        use streamReader = new StreamReader(stream)
        streamReader.ReadToEnd() |> Parser.parseLecturesHtml getHtmlAsync

    member __.ActiveCourses = activeCourses
    member __.UpcomingCourses = upcomingCourses
    member __.FinishedCourses = finishedCourses

    member __.CourseTitle = activeCourses.[0].Topic.Name
    member __.LectureSections = lectureSections |> Seq.skip 2   
