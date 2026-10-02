Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Reflection

' Managed replacement for LibResource.GetDropDownListBackground/Border/Mask/PushBackground/
' PushUpdown and the frmResDropDownList resource form. Background/Border/Mask are a single shared
' set (Active vs Deactive only); the push-button art is colour-keyed (13 ColorConstants variants)
' for its EnterFocus/ExitFocus states, plus one shared Disabled bitmap used regardless of colour --
' matches frmResDropDownList.frm (imgPushEnterFocus/imgPushExitFocus are Image arrays indexed
' 0-12; imgBackground/imgDeactiveBackground/imgBorder/imgDeactiveBorder/imgMask/imgPushDisabled/
' imgUpDown/imgUpdownDisable are singular controls).
Namespace Global.Aqua

    Friend Module DropDownListResources

        Private ReadOnly _cache As New Dictionary(Of String, Image)()
        Private ReadOnly _sync As New Object()

        Public Function GetBackground(ByVal active As Boolean) As Image
            Return Load(If(active, "ddl_imgBackground", "ddl_imgDeactiveBackground"))
        End Function

        Public Function GetBorder(ByVal active As Boolean) As Image
            Return Load(If(active, "ddl_imgBorder", "ddl_imgDeactiveBorder"))
        End Function

        Public Function GetMask() As Image
            Return Load("ddl_imgMask")
        End Function

        ''' <summary>Port of GetDropDownListPushBackground: Disabled wins over colour/hover entirely
        ''' (one shared bitmap for every colour), otherwise EnterFocus (hover) vs ExitFocus (idle),
        ''' colour-keyed.</summary>
        Public Function GetPushBackground(ByVal color As ColorConstants, ByVal enabled As Boolean, ByVal hover As Boolean) As Image
            If Not enabled Then Return Load("ddl_imgPushDisabled")
            Dim key As String = If(hover, "ddl_imgPushEnterFocus_", "ddl_imgPushExitFocus_") & CInt(color).ToString("D2")
            Return Load(key)
        End Function

        Public Function GetPushUpDown(ByVal enabled As Boolean) As Image
            Return Load(If(enabled, "ddl_imgUpDown", "ddl_imgUpdownDisable"))
        End Function

        Private Function Load(ByVal key As String) As Image
            SyncLock _sync
                Dim img As Image = Nothing
                If _cache.TryGetValue(key, img) Then Return img
                Using s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Aqua.DropDownListRes." & key & ".bmp")
                    If s Is Nothing Then Return Nothing
                    Using tmp As New Bitmap(s)   ' clone: stream-backed bitmaps fault once the stream closes
                        img = New Bitmap(tmp)
                    End Using
                End Using
                _cache(key) = img
                Return img
            End SyncLock
        End Function

    End Module

End Namespace
