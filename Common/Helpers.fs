[<AutoOpen>]
module Helpers

open System
open System.Globalization
open System.Text.RegularExpressions

let parseInt str = 
    match Int32.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture) with
    | true, i -> Some i
    | false, _ -> None

let trim (s:string) = 
    s.Trim()

let replace (value:string) replacement (str:string) = 
    if str = "" then "" else str.Replace(value, replacement)

let replaceRegex pattern (replacement:string) str = 
    Regex.Replace(str, pattern, replacement)

let remove value (str:string) = 
    if str = "" then "" else str.Replace(value, "")

let trimEnd value (str:string) = 
    if str.EndsWith value then str.Substring(0, str.Length - value.Length) else str

let removeRegex pattern (str:string) = 
    Regex.Replace(str, pattern, "")

let trimAndUnescape (text:string) = 
    text
    |> replace "&nbsp;" " "
    |> replace "&amp;" "&"
    |> replace "&quot;" "\""
    |> replace "apos;" "'"
    |> replace "&lt;" "<"
    |> replace "&gt;" ">"
    |> trim
