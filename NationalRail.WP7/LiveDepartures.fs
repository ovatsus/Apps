module NationalRail.LiveDepartures

open System
open System.Reflection
open System.IO
open System.Text
open HtmlAgilityPack
open HtmlAgilityPack.FSharp
open FSharp.Control
open FSharp.Data.Csv
open FSharp.Data.Csv.Extensions
open FSharp.GeoUtils
open FSharp.Net

let private stationInfo = ref None

let getStationInfo() = 

    match !stationInfo with
    | None ->

        let allStations = 
            //from http://www.data.gov.uk/dataset/naptan
            use stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("RailReferences.csv")
            let csvFile = CsvFile.Load stream
            csvFile.Data
            |> Seq.groupBy (fun station -> station?CrsCode)
            |> Seq.map (fun (code, stations) -> 
                let station = 
                    stations 
                    |> Seq.sortBy (fun station -> -station?RevisionNumber.AsInteger()) 
                    |> Seq.head
                { Code = code
                  Name = station?StationName.Replace(" Rail Station", null)
                  LatLong = LatLong.FromUTM (station?Easting.AsInteger()) (station?Northing.AsInteger()) })
            |> Seq.toList

        let stationsByCode =
            allStations 
            |> Seq.map (fun station -> station.Code, station) 
            |> dict 

        stationInfo := Some (allStations, stationsByCode)
        allStations, stationsByCode

    | Some stationInfo -> stationInfo

let getNearestStations currentLocation limit = 
    
    async {

        let allStations, _ = getStationInfo()

        let nearestStations =
            allStations
            |> Seq.map (fun station -> station.LatLong - currentLocation, station)
            |> Seq.filter (fun (dist, _) -> dist < 1000.0)
            |> Seq.sortBy fst
            |> Seq.truncate limit
            |> Seq.map (fun (distance, station) -> sprintf "%.1f km" distance, station)
            |> Seq.toArray

        return nearestStations

    } |> LazyAsync.fromAsync

let getStation stationCode =
    let _, stationsByCode = getStationInfo()
    stationsByCode.[stationCode]

let getDepartures toStation fromStation =

    let getStatus due (statusCell:HtmlNode) = 
        if statusCell.InnerText.Trim() = "Cancelled" then
            Cancelled, None
        else
            let statusSpan = statusCell.Element("span")
            if statusSpan <> null && statusSpan.InnerText.Contains(" mins late") then
                let delayMins = statusCell.Element("span").InnerText.Replace(" mins late", "") |> Int32.Parse
                Delayed delayMins, due + { Hours = 0; Minutes = delayMins } |> Some
            else
                OnTime, None

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
        let status, _ = cells.[2] |> getStatus departs
        { Departs = departs
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
            |> Seq.toList }

    let rowToDeparture (tr:HtmlNode) =
        let cells = tr.Elements "td" |> Seq.toArray        
        let destination, via = 
            let dest = cells.[1].InnerText.Trim()
            let pos = dest.IndexOf " via"
            if pos = -1
            then dest, ""
            else dest.Substring(0, pos), dest.Substring(pos + 1)
        let due = cells.[0] |> parseTime
        let status, expected = cells.[2] |> getStatus due
        { Due = due
          Expected = expected
          Destination = destination
          Via = via
          Status = status
          Platform = cells.[3] |> parsePlatform
          Details = 
            let details = cells.[4] |> element "a" |> attr "href"
            LazyAsync.fromAsync (getJourneyDetails ("http://ojp.nationalrail.co.uk" + details)) }

    let url = 
        match toStation with
        | None -> "http://ojp.nationalrail.co.uk/service/ldbboard/dep/" + fromStation.Code
        | Some toStation -> "http://ojp.nationalrail.co.uk/service/ldbboard/dep/" + fromStation.Code + "/" + toStation.Code + "/To"

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

let downloadSchedule (username:string) (password:string) (toc:Toc) (schedule:ScheduleType) day = 
    let url = sprintf "https://datafeeds.networkrail.co.uk/ntrod/CifFileAuthenticate?type=CIF_%s_%s_DAILY&day=%s" 
                      (toc.ToUrl()) 
                      (schedule.ToUrl()) 
                      (schedule.ToUrl(day))    
    let auth = "Basic " + (username + ":" + password |> Encoding.UTF8.GetBytes |> Convert.ToBase64String)
    Http.Request(url, headers=["Authorization", auth])
