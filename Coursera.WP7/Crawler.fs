namespace Coursera

open System
open System.Collections.Generic
open System.Net
open System.Text
open FSharp.Control
open FSharp.Net
open FSharp.Data.Json
open FSharp.Data.Json.Extensions

[<AutoOpen>]
module private Implementation = 

    let getCsrfToken() = 
        let sb = new StringBuilder()
        let random = new Random()
        let chars = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
        for i in 0..23 do
            sb.Append chars.[random.Next(chars.Length)] |> ignore
        sb.ToString()

    let getHomeUrl courseBaseUrl = courseBaseUrl + "class/index"
    let getLecturesUrl courseBaseUrl = courseBaseUrl + "lecture/index"
    let getLoginUrl courseBaseUrl = courseBaseUrl + "auth/auth_redirector?type=login&subtype=normal"

    let login email password = async {
        let csrfToken = getCsrfToken()
        let cookieContainer = new CookieContainer()
        let! _ = Http.AsyncRequest("https://accounts.coursera.org/api/v1/login",
                                   headers = ["Origin", "https://accounts.coursera.org"
                                              "X-CSRFToken", csrfToken
                                              "Referer", "https://accounts.coursera.org/signin"], 
                                   bodyValues = ["email", email
                                                 "password", password],
                                   cookies = ["csrftoken", csrfToken],
                                   cookieContainer = cookieContainer)
        return cookieContainer }

    let getTopicsJson email password cacheGet cacheSet =
        let url = "https://www.coursera.org/maestro/api/topic/list_my"
        match cacheGet url with
        | Some html -> async.Return html
        | None -> async {
            let! cookieContainer = login email password
            let! json = Http.AsyncRequest(url, cookieContainer = cookieContainer) 
            cacheSet url json
            return json }

    let getCrawler email password courseBaseUrl cacheGet cacheSet = 
        let cookieContainer = ref None
        fun url -> 
            match cacheGet url with
            | Some html -> async.Return html
            | None -> async {
                if (!cookieContainer).IsNone then
                    let! cc = login email password
                    let! _ = Http.AsyncRequest(getLoginUrl courseBaseUrl, cookieContainer = cc)
                    cookieContainer := Some cc
                let! html = Http.AsyncRequest(url, cookieContainer = (!cookieContainer).Value)
                cacheSet url html
                return html }

type Crawler(email, password, cache:IDictionary<_,_>, cacheSet:Action<_,_>) =

    let urlToFilename (url:string) = 
        url.Replace("https://www.coursera.org/", null)
           .Replace("https://class.coursera.org/", null)
           .Replace("/", "_")
           .Replace("?", "_")

    let cacheGet url =
        let filename = urlToFilename url 
        match cache.TryGetValue(filename) with
        | true, content -> Some content
        | false, _ -> None

    let cacheSet url content = 
        let filename = urlToFilename url 
        cache.[url] <- content
        cacheSet.Invoke(filename, content)

    let coursesById = ref None

    let getLectureSections forceRefresh courseBaseUrl =
        let lecturesUrl = getLecturesUrl courseBaseUrl
        let cacheGet = 
            if forceRefresh then
                fun _ -> None
            else
                cacheGet
        let crawler = getCrawler email password courseBaseUrl cacheGet cacheSet
        let sections = 
            (crawler (getLecturesUrl courseBaseUrl))
            |> LazyAsync.fromAsync
            |> LazyAsync.map (Parser.parseLecturesHtml crawler)
        sections

    let parseTopicsJson topicsJson = 
        let courses = 
            topicsJson
            |> Parser.parseTopicsJson (getLectureSections false)
        coursesById :=
            (let dict = Dictionary()
             for course in courses do
                 dict.Add(course.Id, course)
             Some dict)
        courses

    let getCourses forceRefreshOfCourseList = 
        let cacheGet = 
            if forceRefreshOfCourseList then
                fun _ -> None
            else
                cacheGet
        getTopicsJson email password cacheGet cacheSet
        |> LazyAsync.fromAsync 
        |> LazyAsync.map parseTopicsJson

    let courses = ref (getCourses false)

    member __.Courses = !courses

    member __.RefreshCourses() =
        courses := getCourses true

    member __.GetCourse(courseId) = 
        match !coursesById with
        | Some coursesById -> coursesById.[courseId]
        | None -> failwithf "Courses not fetched yet"

    member x.RefreshCourse(courseId) =
        match !coursesById with
        | Some coursesById -> 
            let course = x.GetCourse(courseId)
            let lecturesSection = getLectureSections true course.HomeLink
            let refreshedCourse = { course with LectureSections = lecturesSection }
            coursesById.[courseId] <- refreshedCourse
            refreshedCourse
        | None -> failwithf "Courses not fetched yet"
