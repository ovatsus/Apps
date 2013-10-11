namespace Coursera

open FSharp.Control

type Course =
    { Id : int
      Name : string
      StartDate : string
      Duration : string
      HomeLink : string
      Active : bool 
      HasFinished : bool 
      Topic : Topic 
      LectureSections : LazyAsync<LectureSection[]> }

    override x.ToString() = sprintf "%A" x

and Topic = 
    { Display : bool
      Id : int
      Instructor : string
      Language : string
      LargeIcon : string
      Name : string
      Photo : string
      PreviewLink : string
      SelfServiceCourseId : int option
      ShortDescription : string
      ShortName : string
      SmallIcon : string
      SmallIconHover : string
      Visible : bool }

    override x.ToString() = sprintf "%A" x

and LectureSection =
    { Title : string
      Completed : bool
      Lectures : Lecture[] }

    override x.ToString() = sprintf "%A" x

and Lecture = 
    { Id : int
      Title : string
      VideoUrl : LazyAsync<string>
      LectureNotesUrl : string
      Viewed : bool 
      QuizAttempted : bool }

    member x.HasLectureNotes = x.LectureNotesUrl <> ""
    override x.ToString() = sprintf "%A" x
