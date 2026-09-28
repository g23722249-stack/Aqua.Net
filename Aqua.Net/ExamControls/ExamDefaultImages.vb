Option Strict On
Option Explicit On

Imports System.Drawing

Namespace Global.Aqua

    ''' <summary>
    ''' The default artwork of CheckBox / RadioButton / Button (ExamControls), taken from frmResource once
    ''' per process and shared by every instance. Each constructor used to create a whole frmResource
    ''' form -- decoding all of its pictures -- just to take four of them: about 3 ms per control, which
    ''' made a MediaItem (it holds a CheckBox) cost 4 ms and a 1000-photo MediaList 4 s to fill.
    ''' The images are never disposed: tinting (the design-time colour editor) makes new ones.
    ''' </summary>
    Friend NotInheritable Class ExamDefaultImages

        Public Shared CheckUnChecked, CheckChecked, CheckUnCheckDisabled, CheckDisabled As Image
        Public Shared RadioUnChecked, RadioChecked, RadioUnCheckDisabled, RadioDisabled As Image
        Public Shared ButtonNormal, ButtonHover, ButtonPressed, ButtonDisabled As Image

        Private Shared _loaded As Boolean
        Private Shared ReadOnly _sync As New Object()

        Private Sub New()
        End Sub

        ''' <summary>Loads the images on first use (Form.Dispose doesn't dispose its pictures' Images).</summary>
        Public Shared Sub EnsureLoaded()
            SyncLock _sync
                If _loaded Then Return
                Using frmRes As New frmResource()
                    CheckUnChecked = frmRes.picCheckImageUnChecked.Image
                    CheckChecked = frmRes.picCheckImageChecked.Image
                    CheckUnCheckDisabled = frmRes.picCheckImageUnCheckDisabled.Image
                    CheckDisabled = frmRes.picCheckImageDisabled.Image
                    RadioUnChecked = frmRes.picRadioImageUnChecked.Image
                    RadioChecked = frmRes.picRadioImageChecked.Image
                    RadioUnCheckDisabled = frmRes.picRadioImageUnCheckDisabled.Image
                    RadioDisabled = frmRes.picRadioImageDisabled.Image
                    ButtonNormal = frmRes.picButtonImageNormal.Image
                    ButtonHover = frmRes.picButtonImageHover.Image
                    ButtonPressed = frmRes.picButtonImagePressed.Image
                    ButtonDisabled = frmRes.picButtonImageDisabled.Image
                End Using
                _loaded = True
            End SyncLock
        End Sub

    End Class

End Namespace
