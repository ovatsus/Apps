namespace NationalRail

open System
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
        assert not x.HasDestinationFilter
        { Station = x.Station
          CallingAt = None }

type Departure = {
    Due : Time
    Expected : Time option
    Destination : string
    DestinationDetail : string
    Status : Status
    Platform : string option
    Details : LazyAsync<JourneyElement[]>
}

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
}

and JourneyElementStatus =
    | OnTime of (*departed*)bool
    | NoReport
    | Delayed of (*departed*)bool * int
    override x.ToString() =
        match x with
        | OnTime _ -> "On time"
        | NoReport -> "No report"
        | Delayed (_, mins) -> sprintf "Delayed %d mins" mins
    member x.HasDeparted =
        match x with
        | OnTime hasDeparted -> hasDeparted
        | NoReport -> true
        | Delayed (hasDeparted, _) -> hasDeparted

type DepartureType = 
    | Departure
    | Arrival
    override x.ToString() = 
        match x with
        | Departure -> "dep"
        | Arrival -> "arr"

type Departure with
    member x.PlatformIsKnown = x.Platform.IsSome

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

        let getStatus due (statusCell:HtmlNode) = 
            if statusCell.InnerText.Trim() = "Cancelled" then
                Status.Cancelled, None
            else
                let statusSpan = statusCell.Element("span")
                if statusSpan <> null && statusSpan.InnerText.Contains(" mins late") then
                    let delayMins = statusSpan.InnerText.Replace(" mins late", "") |> Int32.Parse
                    Status.Delayed delayMins, due + { Hours = 0; Minutes = delayMins } |> Some
                else
                    Status.OnTime, None

        let getJourneyElementStatus due (statusCell:HtmlNode) = 
            if statusCell.InnerText.Trim() = "No report" then
                NoReport, None
            else
                let statusSpan = statusCell.Element("span")
                let hasDeparted = statusSpan <> null && statusSpan.InnerText = "Departed"
                let statusSpan = statusCell.Elements("span").Last()
                if statusSpan <> null && statusSpan.InnerText.Contains(" mins late") then
                    let delayMins = statusSpan.InnerText.Replace(" mins late", "") |> Int32.Parse
                    Delayed (hasDeparted, delayMins), due + { Hours = 0; Minutes = delayMins } |> Some
                else
                    OnTime hasDeparted, None

        let parseTime (cell:HtmlNode) = 
            let time = cell.InnerText
            let pos = time.IndexOf ':'
            let hours = time.Substring(0, pos) |> Int32.Parse
            let minutes = time.Substring(pos+1) |> Int32.Parse
            { Hours = hours
              Minutes = minutes }

        let parsePlatform (cell:HtmlNode) = 
            match cell.InnerText.Trim() with
            | "" -> None
            | platform -> platform |> Some

        let rowToJourneyElement (tr:HtmlNode) = 
            let cells = tr.Elements "td" |> Seq.toArray
            let departs = cells.[0] |> parseTime
            let status, expected = cells.[2] |> getJourneyElementStatus departs
            { Departs = departs
              Expected = expected
              Station = cells.[1].InnerText.Trim()
              Status = status
              Platform = cells.[3] |> parsePlatform}

        let getJourneyDetails url = async {
        
            let! html = Http.AsyncRequest url
            let doc = createDoc html   
        
            return 
                doc 
                |> descendants "tbody" 
                |> Seq.head
                |> elements "tr"
                |> Seq.map rowToJourneyElement
                |> Seq.toArray }

        let rowToDeparture (tr:HtmlNode) =
            let cells = tr.Elements "td" |> Seq.toArray        
            let destination, destinationDetail = 
                let dest = cells.[1].InnerText.Trim()
                let dest = Regex.Replace(dest, "\s+", " ")
                let pos = dest.IndexOf " via"
                if pos = -1
                then 
                    let pos = dest.IndexOf " (circular route)"
                    if pos = -1
                    then dest, ""
                    else dest.Substring(0, pos), dest.Substring(pos + 1)
                else dest.Substring(0, pos), dest.Substring(pos + 1)
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
            let doc = createDoc html   

            let departures = 
                doc 
                |> descendants "tbody" 
                |> Seq.collect (fun body -> body |> elements "tr")                
                |> Seq.map rowToDeparture
                |> Seq.toArray

            return departures } |> LazyAsync.fromAsync
