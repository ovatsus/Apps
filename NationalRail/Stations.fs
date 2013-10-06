namespace NationalRail

open System.Reflection
open FSharp.Control
open FSharp.Data
open FSharp.GeoUtils

type Station =
    { Code : string
      Name : string
      Location : LatLong }

module Stations = 

    let private stationInfo = ref None

    let private getStationInfo() = 

        match !stationInfo with
        | None ->

            let allStations = 
                //from http://www.data.gov.uk/dataset/naptan
                //osgb36 to latitude/longitude converted with http://gridreferencefinder.com/batchConvert/batchConvert.htm
                use stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("RailReferences.csv")
                let csvFile = CsvProvider<"RailReferences.csv", Schema="Latitude=float,Longitude=float">.Load stream
                csvFile.Data
                |> Seq.groupBy (fun station -> station.CrsCode)
                |> Seq.map (fun (code, stations) -> 
                    let station = 
                        stations 
                        |> Seq.sortBy (fun station -> -station.RevisionNumber) 
                        |> Seq.head
                    { Code = code
                      Name = station.StationName.Replace(" Rail Station", null).Replace(" Railway Station", null)
                      Location = LatLong.Create station.Latitude station.Longitude })
                |> Seq.toList

            let stationsByCode =
                allStations 
                |> Seq.map (fun station -> station.Code, station) 
                |> dict 

            stationInfo := Some (allStations, stationsByCode)
            allStations, stationsByCode

        | Some stationInfo -> stationInfo

    [<CompiledName("GetNearest")>]
    let getNearest currentPosition limit = 
    
        async {

            let allStations, _ = getStationInfo()

            let nearestStations =
                allStations
                |> Seq.map (fun station -> station.Location - currentPosition, station)
                |> Seq.filter (fun (dist, _) -> dist < 9.5)
                |> Seq.sortBy (fun (dist, station) -> dist, station.Name)
                |> Seq.truncate limit
                |> Seq.toArray

            return nearestStations

        } |> LazyAsync.fromAsync

    [<CompiledName("GetAll")>]
    let getAll() =     
        let allStations, _ = getStationInfo()
        allStations
        |> Seq.sortBy (fun station -> station.Name)
        |> Seq.toArray

    [<CompiledName("Get")>]
    let get stationCode =
        let _, stationsByCode = getStationInfo()
        stationsByCode.[stationCode]