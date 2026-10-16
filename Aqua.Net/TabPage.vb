Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.Drawing
Imports System.Windows.Forms

' One page of an AquaTabControl: a panel that hosts the page's controls and paints the
' Aqua page-sheet background. Analogous to the VB6 PageSheet's content area.
Namespace Global.Aqua

    <DefaultProperty("Title")>
    Public Class TabPage
        ' WinForms' Panel, spelled out: inside the Aqua namespace a bare "Panel" is Aqua.Panel, whose
        ' Image / SizeMode / PanelStyle / BorderColor a page never used (it paints the sheet texture
        ' itself) but showed in the designer -- and the VB6 conversion filled them in.
        Inherits System.Windows.Forms.Panel

        Private _title As String = "Page"

        Public Sub New()
            SetStyle(ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
            DoubleBuffered = True
            BackColor = Color.White
        End Sub

        Public Sub New(ByVal title As String)
            Me.New()
            _title = If(title, "")
        End Sub

        ''' <summary>The caption shown on this page's tab.</summary>
        <DefaultValue("Page")>
        Public Property Title As String
            Get
                Return _title
            End Get
            Set(value As String)
                _title = If(value, "")
                Dim tc = TryCast(Parent, TabControl)
                If tc IsNot Nothing Then tc.Invalidate()
            End Set
        End Property

        Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
            Dim bg As Image = Theme.Skin(PageResources.GetSheetBackground())
            If bg IsNot Nothing Then
                ' tile the texture to fill (not stretch)
                Using tb As New TextureBrush(bg, Drawing2D.WrapMode.Tile)
                    e.Graphics.FillRectangle(tb, ClientRectangle)
                End Using
            Else
                MyBase.OnPaintBackground(e)
            End If
        End Sub

    End Class

End Namespace
