#r "System.Net"
#r "../lib/portable/FSharp.Data.dll"

open System
open System.IO
open FSharp.Net

let js = Http.RequestString("http://erail.in/js/cmp/erail_all_33.js")
let startStr = "var StationsData=\""
let startPos = js.IndexOf startStr + startStr.Length
let endPos = js.IndexOf('"', startPos)

let parts = js.Substring(startPos, endPos - startPos).Split(',') 

let stations = [| for i in 0..2..parts.Length-2 -> parts.[i], parts.[i+1] |]

File.WriteAllLines(
    __SOURCE_DIRECTORY__ + "/IndiaStations.csv", 
    stations
    |> Seq.map (fun (code, name) -> name + "," + code)
    |> Seq.append ["Name, Code"])
