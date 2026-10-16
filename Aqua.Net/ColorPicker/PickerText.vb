' Aqua.Net -- ColorPicker 的介面文字翻譯掛鉤
'
' 色彩選擇器裡的文字是繁體中文原文；主程式可以設定 Translate 換成其他語言
' （PhotoEdit 設成 Lang.T）。沒設定時照原文顯示（iPhoto 不受影響）。
' 要在第一次使用 ColorPicker 之前設定（有些名稱表是共用的靜態欄位）。

Imports System.Globalization

Namespace Global.Aqua

    Public Module PickerText

        ''' <summary>翻譯函式：輸入繁體原文，傳回要顯示的文字。Nothing＝照原文。</summary>
        Public Property Translate As Func(Of String, String)

        Friend Function Tr(text As String) As String
            Dim f = Translate
            If f Is Nothing Then Return text
            Return f(text)
        End Function

        Friend Function TrF(text As String, ParamArray args As Object()) As String
            Return String.Format(CultureInfo.CurrentCulture, Tr(text), args)
        End Function
    End Module

End Namespace
