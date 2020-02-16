module Main exposing (main)

import Api
import Browser
import Html exposing (Html)
import Http
import Json.Decode exposing (Value)
import Task


type alias Flags =
    Value


type alias Model =
    List (List (List String))


type alias Msg =
    Result Http.Error Model


main : Program Flags Model Msg
main =
    Browser.element
        { init = init
        , view = view
        , update = update
        , subscriptions = subscriptions
        }


init : Flags -> ( Model, Cmd Msg )
init _ =
    ( [ [ [ "Asking" ] ] ], Task.attempt identity <| Api.getFiles3 identity )


update : Msg -> Model -> ( Model, Cmd Msg )
update msg _ =
    case msg of
        Ok listlistlist ->
            ( listlistlist, Cmd.none )

        Err _ ->
            ( [ [ [ "Error" ] ] ], Cmd.none )


view : Model -> Html Msg
view model =
    let
        list viewItem lst =
            Html.ul [] <| List.map (\i -> Html.li [] [ viewItem i ]) lst
    in
    list (list (list Html.text)) model


subscriptions : Model -> Sub Msg
subscriptions _ =
    Sub.none
