#r "System.Net"
#r "../lib/portable/FSharp.Data.dll"
#r "../packages/HtmlAgilityPack-PCL.1.4.6/lib/HtmlAgilityPack-PCL.dll"

#load "HtmlAgilityPack.FSharp.fs"

open System
open FSharp.Net
open HtmlAgilityPack
open HtmlAgilityPack.FSharp

// get a page that lists the stations that start with firstLetter
let getStationListPage firstLetter = 
    "http://www.irishrail.ie/cat_stations_list.jsp?letter=" + (string firstLetter)
    |> Http.AsyncRequestString

// get all the links to stations inside the <ul class="results">
let getStations stationListPage =
    stationListPage
    |> createDoc
    |> descendants "ul"
    |> Seq.filter (hasClass "results")
    |> Seq.head
    |> descendants "a"
    |> Seq.map (attr "href")
    |> Seq.toArray

// get the page for a station
let getStationPage station =
    "http://www.irishrail.ie/" + station
    |> Http.AsyncRequestString

// get the latitude and longitude of a station from the google maps link in the station page
let getCodeAndCoordinates stationPage = 
    let split (c:char) (s:string) = s.Split c
    let getUrlParam key (url:string) = 
        url.Substring (url.IndexOf '?' + 1)
        |> split '&'
        |> Seq.map (split '=')
        |> Seq.filter (Seq.head >> (=) key)
        |> Seq.head
        |> Seq.skip 1
        |> Seq.head
    let code = 
        stationPage
        |> createDoc
        |> descendants "iframe"
        |> Seq.last
        |> attr "src"        
        |> getUrlParam "ref"
    let [| lat; long |] = 
        stationPage
        |> createDoc
        |> descendants "div"
        |> Seq.filter (hasId "map-ordnance")
        |> Seq.head
        |> followingSibling "ul"
        |> descendants "a"
        |> Seq.head
        |> attr "href"
        |> getUrlParam "ll"
        |> split ',' 
        |> Array.map float
    code, lat, long

let stationsAndCoords =
    let stations = 
        ['A'..'Z'] 
        |> Seq.map getStationListPage
        |> Async.Parallel
        |> Async.RunSynchronously
        |> Array.collect getStations
    let codeAndCoordinates = 
        stations
        |> Seq.map getStationPage
        |> Async.Parallel
        |> Async.RunSynchronously
        |> Array.map getCodeAndCoordinates
    let stations = 
        stations
        |> Array.map Uri.UnescapeDataString
    Array.map2 (fun station (code, lat, long) -> station, code, lat, long) stations codeAndCoordinates

open System.IO

File.WriteAllLines(
    __SOURCE_DIRECTORY__ + "/IrelandStations.csv", 
    stationsAndCoords 
    |> Seq.map (fun (name, code, lat, long) -> name + "," + code + "," + (string lat) + "," + (string long))
    |> Seq.append ["Name, Code, Latitude (float), Longitude (float)"])
