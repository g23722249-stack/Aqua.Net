Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Windows.Forms

Namespace Global.Aqua

    ''' <summary>
    ''' 「常用」頁:上面 3 列 × 7 格常用色(右鍵可改成目前顏色或全部還原),下面 7 格最近使用色(新到舊)。
    ''' 原本放在色環下方,獨立成頁籤後選色器可以縮小。
    ''' </summary>
    Friend NotInheritable Class FavoritesPage
        Inherits Control

        Private Const CaptionHeight As Integer = 20
        Private Const Columns As Integer = 7

        Private ReadOnly _custom As SwatchGrid
        Private ReadOnly _recent As SwatchGrid

        Public Sub New(ByVal custom As SwatchGrid, ByVal recent As SwatchGrid)
            SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.UserPaint Or
                     ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
            _custom = custom
            _recent = recent
            Controls.Add(_custom)
            Controls.Add(_recent)
        End Sub

        Private ReadOnly Property Cell As Integer
            Get
                Return Math.Max(12, Width \ Columns)
            End Get
        End Property

        Private ReadOnly Property RecentCaptionTop As Integer
            Get
                Return CaptionHeight + Cell * 3 + 8
            End Get
        End Property

        Protected Overrides Sub OnLayout(ByVal e As LayoutEventArgs)
            MyBase.OnLayout(e)
            Dim c As Integer = Cell
            _custom.SetBounds(0, CaptionHeight, c * Columns, c * 3)
            _recent.SetBounds(0, RecentCaptionTop + CaptionHeight, c * Columns, c)
            Invalidate()
        End Sub

        Protected Overrides Sub OnPaint(ByVal e As PaintEventArgs)
            Dim g As Graphics = e.Graphics
            Dim scheme As PickerTheme = PickerTheme.Of(Me)
            g.Clear(scheme.Back)
            Using head As New SolidBrush(scheme.Heading), hint As New SolidBrush(scheme.SubText),
                  bold As New Font(Font, FontStyle.Bold),
                  sf As New StringFormat() With {.LineAlignment = StringAlignment.Center, .Trimming = StringTrimming.EllipsisCharacter}
                DrawCaption(g, "常用色", "右鍵可設為目前顏色", 0, bold, head, hint, sf)
                DrawCaption(g, "最近使用", "新到舊", RecentCaptionTop, bold, head, hint, sf)
            End Using
        End Sub

        Private Sub DrawCaption(ByVal g As Graphics, ByVal title As String, ByVal note As String, ByVal y As Integer,
                                ByVal bold As Font, ByVal head As Brush, ByVal hint As Brush, ByVal sf As StringFormat)
            Dim w As Single = g.MeasureString(title, bold).Width
            g.DrawString(title, bold, head, New RectangleF(2, y, w + 4, CaptionHeight), sf)
            ' 窄的時候(精簡版)放不下說明就不寫,不要擠成兩行
            Dim room As Single = Width - w - 12
            If g.MeasureString(note, Font).Width <= room Then
                g.DrawString(note, Font, hint, New RectangleF(w + 10, y, room, CaptionHeight), sf)
            End If
        End Sub

    End Class

End Namespace
