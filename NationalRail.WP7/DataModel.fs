namespace NationalRail

open System
open FSharp.Control
open FSharp.GeoUtils

type Station =
    { Code : string
      Name : string
      LatLong : LatLong }
 
type Departure = {
    Due : Time
    Expected : Time option
    Destination : string
    Via : string
    Status : Status
    Platform : string option
    Details : LazyAsync<JourneyElement list>
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
    Station : string
    Status : Status
    Platform : string option
}

type ScheduleType = 
    | Full
    | DailyUpdate
    member x.ToUrl() = 
        match x with 
        | DailyUpdate -> "UPDATE"
        | Full -> "FULL"
    member x.ToUrl(day:DayOfWeek) =
        match x with 
        | DailyUpdate -> "toc-update-" + day.ToString().Substring(0,3).ToLowerInvariant()
        | Full -> "toc-full"

type Toc = 
    | All
    | ArrivaTrainsWales
    | C2C
    | CrossCountry
    | DevonAndCornwall
    | EastMidlandsTrains
    | Eurostar
    | FfestiniogRailway
    | FirstCapitalConnect
    | FirstGreatWestern
    | FirstHullTrains
    | FirstScotrail
    | FirstTranspennineExpress
    | GatwickExpress
    | GrandCentral
    | HeathrowConnect
    | HeathrowExpress
    | IslandLines
    | LondonMidland
    | LondonOverground
    | LULBakerlooLine
    | LULDistrictLineRichmond
    | LULDistrictLineWimbledon
    | Merseyrail
    | NationalExpressEastAnglia
    | NationalExpressEastCoast
    | Nexus
    | NorthYorkshireMoorsRailway
    | NorthernRail
    | Southeastern
    | Southern
    | StagecoachSouthWesternTrains
    | Chiltern
    | VirginWestCoast
    | WestCoastRailway
    | WrexhamAndShropshire
    member x.ToUrl() =
        match x with
        | All -> "ALL"        
        | ArrivaTrainsWales -> "HL"
        | C2C -> "HT"
        | CrossCountry -> "EH"
        | DevonAndCornwall -> "EN"
        | EastMidlandsTrains -> "EM"
        | Eurostar -> "GA"
        | FfestiniogRailway -> "XJ"
        | FirstCapitalConnect -> "EG"
        | FirstGreatWestern -> "EF"
        | FirstHullTrains -> "PF"
        | FirstScotrail -> "HA"
        | FirstTranspennineExpress -> "EA"
        | GatwickExpress -> "HV"
        | GrandCentral -> "EC"
        | HeathrowConnect -> "EE"
        | HeathrowExpress -> "HM"
        | IslandLines -> "HZ"
        | LondonMidland -> "EJ"
        | LondonOverground -> "EK"
        | LULBakerlooLine -> "XC"
        | LULDistrictLineRichmond -> "XE"
        | LULDistrictLineWimbledon -> "XB"
        | Merseyrail -> "HE"
        | NationalExpressEastAnglia -> "EB"
        | NationalExpressEastCoast -> "HB"
        | Nexus -> "PG"
        | NorthYorkshireMoorsRailway -> "PR"
        | NorthernRail -> "ED"
        | Southeastern -> "HU"
        | Southern -> "HW"
        | StagecoachSouthWesternTrains -> "HY"
        | Chiltern -> "HO"
        | VirginWestCoast -> "HF"
        | WestCoastRailway -> "PA"
        | WrexhamAndShropshire -> "EI"

