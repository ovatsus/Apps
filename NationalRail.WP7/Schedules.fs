namespace NationalRail

open System
open System.Text
open FSharp.Net

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

module Schedules = 

    let download (username:string) (password:string) (toc:Toc) (schedule:ScheduleType) day = 
        let url = sprintf "https://datafeeds.networkrail.co.uk/ntrod/CifFileAuthenticate?type=CIF_%s_%s_DAILY&day=%s" 
                          (toc.ToUrl()) 
                          (schedule.ToUrl()) 
                          (schedule.ToUrl(day))    
        let auth = "Basic " + (username + ":" + password |> Encoding.UTF8.GetBytes |> Convert.ToBase64String)
        Http.Request(url, headers=["Authorization", auth])
