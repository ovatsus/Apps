namespace NationalRail

open System
open System.Globalization
open System.Linq
open System.Text.RegularExpressions
open FSharp.Control
open FSharp.Net
open HtmlAgilityPack
open HtmlAgilityPack.FSharp

type DeparturesTable = 
    { Station : Station
      CallingAt : Station option }
    override x.ToString() =
        match x.CallingAt with
        | None -> x.Station.Name
        | Some callingAt -> sprintf "%s calling at %s" x.Station.Name callingAt.Name
    member x.HasDestinationFilter = x.CallingAt.IsSome
    member x.WithoutFilter = 
        if not x.HasDestinationFilter then failwith "%A doesn't have a destination filter" x
        { Station = x.Station
          CallingAt = None }
    member x.Reversed =
        if not x.HasDestinationFilter then failwith "%A can't be reversed" x
        { Station = x.CallingAt.Value
          CallingAt = Some x.Station }

type Departure = {
    Due : Time
    Expected : Time option
    Destination : string
    DestinationDetail : string
    Status : Status
    Platform : string option
    Details : LazyAsync<JourneyElement[]>
} with member x.PlatformIsKnown = x.Platform.IsSome

and Time = 
    { Hours : int
      Minutes : int }
    override x.ToString() = sprintf "%02d:%02d" x.Hours x.Minutes
    static member (+) (t1, t2) =
        let hours = t1.Hours + t2.Hours
        let minutes = t1.Minutes + t2.Minutes
        let hours, minutes = 
            if minutes > 59
            then hours + 1, minutes - 60
            else hours, minutes
        let hours = 
            if hours > 23
            then hours - 24
            else hours
        { Hours = hours
          Minutes = minutes }
        
and Status =
    | OnTime
    | Delayed of int
    | Cancelled
    override x.ToString() =
        match x with
        | OnTime -> "On time"
        | Delayed mins -> sprintf "Delayed %d mins" mins
        | Cancelled -> "Cancelled"

and JourneyElement = {
    Departs : Time
    Expected : Time option
    Station : string
    Status : JourneyElementStatus
    Platform : string option
    IsAlternateRoute : bool
}

and JourneyElementStatus =
    | OnTime of (*departed*)bool
    | NoReport
    | Cancelled
    | Delayed of (*departed*)bool * int
    override x.ToString() =
        match x with
        | OnTime _ -> "On time"
        | NoReport -> "No report"
        | Cancelled -> "Cancelled"
        | Delayed (_, mins) -> sprintf "Delayed %d mins" mins
    member x.HasDeparted =
        match x with
        | OnTime hasDeparted -> hasDeparted
        | NoReport -> true
        | Cancelled -> false
        | Delayed (hasDeparted, _) -> hasDeparted

type DepartureType = 
    | Departure
    | Arrival
    override x.ToString() = 
        match x with
        | Departure -> "dep"
        | Arrival -> "arr"

type ParseError(msg, exn) = 
    inherit Exception(msg, exn)

type DeparturesTable with
    
    member x.Serialize() =
        match x.CallingAt with
        | None -> x.Station.Code
        | Some callingAt -> x.Station.Code + "|" + callingAt.Code

    member x.Match(withoutDestinationFilter:Func<_,_>, withDestinationFilter:Func<_,_,_>) =
        match x.CallingAt with
        | None -> withoutDestinationFilter.Invoke(x.Station)
        | Some callingAt -> withDestinationFilter.Invoke(x.Station, callingAt)        

    static member Create(station) =
        { Station = station
          CallingAt = None}

    static member Create(station, callingAt) =
        if obj.ReferenceEquals(callingAt, null) then
            DeparturesTable.Create(station)
        else
            { Station = station 
              CallingAt = callingAt |> Some}
    
    static member Parse (str:string) =
        let pos = str.IndexOf '|'
        if pos >= 0 then
            let station = str.Substring(0, pos) |> Stations.get
            let callingAt = str.Substring(pos + 1) |> Stations.get
            DeparturesTable.Create(station, callingAt)
        else
            let station = str |> Stations.get
            DeparturesTable.Create(station)

    member journey.GetDepartures (departureType:DepartureType) = 

        let parseInt str = 
            match Int32.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture) with
            | true, i -> Some i
            | false, _ -> None

        let getStatus due (statusCell:HtmlNode) = 
            if statusCell.InnerText.Trim() = "Cancelled" then
                Status.Cancelled, None
            else
                let statusSpan = statusCell.Element("span")
                if statusSpan <> null && statusSpan.InnerText.Contains(" mins late") then
                    match statusSpan.InnerText.Replace(" mins late", "") |> parseInt with
                    | Some delayMins -> Status.Delayed delayMins, due + { Hours = 0; Minutes = delayMins } |> Some
                    | _ -> raise <| ParseError(sprintf "Invalid status:\n%s" statusCell.OuterHtml, null)
                else
                    Status.OnTime, None

        let getJourneyElementStatus due (statusCell:HtmlNode) = 
            if statusCell.InnerText.Trim() = "Cancelled" then
                Cancelled, None
            elif statusCell.InnerText.Trim() = "No report" then
                NoReport, None
            else
                let statusSpan = statusCell.Element("span")
                let hasDeparted = statusSpan <> null && (statusSpan.InnerText = "Departed" || statusSpan.InnerText = "Arrived")
                let statusSpan = statusCell.Elements("span").Last()
                if statusSpan <> null && statusSpan.InnerText.Contains(" mins late") then
                    match statusSpan.InnerText.Replace(" mins late", "") |> parseInt with
                    | Some delayMins -> Delayed (hasDeparted, delayMins), due + { Hours = 0; Minutes = delayMins } |> Some
                    | _ -> raise <| ParseError(sprintf "Invalid status:\n%s" statusCell.OuterHtml, null)
                else
                    OnTime hasDeparted, None

        let parseTime (cell:HtmlNode) = 
            let time = cell.InnerText
            let pos = time.IndexOf ':'
            if pos >= 0 then
                let hours = time.Substring(0, pos) |> parseInt
                let minutes = time.Substring(pos+1) |> parseInt
                match hours, minutes with
                | Some hours, Some minutes -> { Hours = hours; Minutes = minutes }
                | _ -> raise <| ParseError(sprintf "Invalid time:\n%s" cell.OuterHtml, null)
            else raise <| ParseError(sprintf "Invalid time:\n%s" cell.OuterHtml, null)

        let parsePlatform (cell:HtmlNode) = 
            match cell.InnerText.Trim() with
            | "" -> None
            | platform -> platform |> Some

        let rowToJourneyElement (tr:HtmlNode) = 
            let cells = tr.Elements "td" |> Seq.toArray
            let departs = cells.[0] |> parseTime
            let status, expected = cells.[2] |> getJourneyElementStatus departs
            let station = cells.[1].InnerText
            let pos = station.IndexOf "Train divides here"
            let station = if pos >= 0 then station.Substring(0, pos) else station
            let isAlternateRoute = tr.Ancestors("tbody") |> Seq.length > 1
            { Departs = departs
              Expected = expected
              Station = (if isAlternateRoute then "* " else "") + station.Trim()
              Status = status
              Platform = cells.[3] |> parsePlatform 
              IsAlternateRoute = isAlternateRoute }

        let getJourneyDetails url = async {
        
            let! html = Http.AsyncRequest url
        
            let getJourneyDetails() = 
                try 
                    createDoc html
                    |> descendants "tbody"
                    |> Seq.collect (fun body -> body |> elements "tr")
                    |> Seq.filter (not << (hasClass "callingpoints"))
                    |> Seq.map rowToJourneyElement
                    |> Seq.toArray
                with 
                | :? ParseError -> reraise()
                | exn -> raise <| ParseError(sprintf "Failed to parse journey details html from %s:\n%s" url (html.Trim()), exn)

            return getJourneyDetails()
        }

        let rowToDeparture (tr:HtmlNode) =
            let cells = tr.Elements "td" |> Seq.toArray        
            let destination, destinationDetail = 
                let dest = cells.[1].InnerText.Trim()
                let dest = Regex.Replace(dest, "\s+", " ")
                let pos = dest.IndexOf " via"
                if pos >= 0
                then dest.Substring(0, pos), dest.Substring(pos + 1)
                else
                    let pos = dest.IndexOf " (circular route)"
                    if pos >= 0
                    then dest.Substring(0, pos), dest.Substring(pos + 1)
                    else dest, ""
            let due = cells.[0] |> parseTime
            let status, expected = cells.[2] |> getStatus due
            { Due = due
              Expected = expected
              Destination = destination
              DestinationDetail = destinationDetail
              Status = status
              Platform = cells.[3] |> parsePlatform
              Details = 
                let details = cells.[4] |> element "a" |> attr "href"
                LazyAsync.fromAsync (getJourneyDetails ("http://ojp.nationalrail.co.uk" + details)) }

        let url = 
            match journey.CallingAt with
            | None -> sprintf "http://ojp.nationalrail.co.uk/service/ldbboard/%s/%s" (departureType.ToString()) journey.Station.Code
            | Some callingAt -> sprintf "http://ojp.nationalrail.co.uk/service/ldbboard/%s/%s/%s/To" (departureType.ToString()) journey.Station.Code callingAt.Code 

        async {
            let! html = Http.AsyncRequest url

            let getDepartures() = 
                try 
                    createDoc html
                    |> descendants "tbody"
                    |> Seq.collect (fun body -> body |> elements "tr")
                    |> Seq.map rowToDeparture
                    |> Seq.toArray
                with 
                | :? ParseError -> reraise()
                | exn -> raise <| ParseError(sprintf "Failed to parse departures html from %s:\n%s" url (html.Trim()), exn)

            return getDepartures()

        } |> LazyAsync.fromAsync
