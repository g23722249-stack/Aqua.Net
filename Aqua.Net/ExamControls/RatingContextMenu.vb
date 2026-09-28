Option Strict Off
Imports System.ComponentModel

Namespace Global.Aqua

<DefaultEvent("RatingChanged")>
Public Class RatingContextMenu
    Inherits ContextMenuStrip

    Public Event RatingChanged(Rating As Integer)

    Private _maxStars As Integer = 5
    Private _selectedRating As Integer = 0
    Private _starSize As Integer = 28
    Private _spacing As Integer = 8
    Private _ratingItem As ToolStripMenuItem   ' 專門畫星星的項目

    ' 設計工具/工具箱需要真正無參數的建構式 (Optional 參數在反射層面仍算一個參數)
    Public Sub New()
        Me.New(5)
    End Sub

    Public Sub New(maxStars As Integer)
        MyBase.New()
        _maxStars = maxStars
        Me.Renderer = New RatingRenderer(Me)
        Me.ShowImageMargin = False
        ' AutoSize=False，改由 OnLayout 依所有項目自動計算下拉尺寸
        Me.AutoSize = False

        ' 依星星數量/大小計算評分項目所需尺寸
        Dim itemWidth As Integer = 8 + _maxStars * (_starSize + _spacing)
        Dim itemHeight As Integer = _starSize + 12

        ' 星星改在 OnRenderMenuItemBackground 繪製 (每個項目必觸發)，
        ' 因此文字留空，避免預設文字疊在星星上。
        _ratingItem = New ToolStripMenuItem() With {
            .AutoSize = False,
            .Size = New Size(itemWidth, itemHeight)
        }
        Me.Items.Add(_ratingItem)
    End Sub

    ' AutoSize=False 時，ContextMenuStrip 不會自動配合項目大小，
    ' 因此於版面配置時依所有項目自行計算並設定下拉尺寸。
    Protected Overrides Sub OnLayout(e As LayoutEventArgs)
        MyBase.OnLayout(e)

        Dim maxWidth As Integer = 0
        Dim totalHeight As Integer = 0
        For Each it As ToolStripItem In Me.Items
            Dim w As Integer, h As Integer
            If it Is _ratingItem Then
                w = _ratingItem.Width
                h = _ratingItem.Height
            Else
                Dim ps As Size = it.GetPreferredSize(New Size(Integer.MaxValue, Integer.MaxValue))
                w = ps.Width
                h = ps.Height
            End If
            maxWidth = Math.Max(maxWidth, w)
            totalHeight += h
        Next

        Dim desired As New Size(maxWidth + Me.Padding.Horizontal + 4, totalHeight + Me.Padding.Vertical + 4)
        If Me.Size <> desired Then Me.Size = desired   ' 加上判斷避免無限重排
    End Sub

    Public ReadOnly Property SelectedRating As Integer
        Get
            Return _selectedRating
        End Get
    End Property

    Friend Sub UpdateRating(rating As Integer)
        _selectedRating = rating
        RaiseEvent RatingChanged(rating)
        Me.Invalidate()
    End Sub

    ' 依項目範圍計算每顆星的矩形 (水平/垂直置中)；繪製與點擊命中共用同一套座標
    Private Function GetStarRects(bounds As Rectangle) As Rectangle()
        Dim stepX As Integer = _starSize + _spacing
        Dim totalWidth As Integer = _maxStars * _starSize + (_maxStars - 1) * _spacing
        Dim startX As Integer = bounds.Left + Math.Max(0, (bounds.Width - totalWidth) \ 2)
        Dim top As Integer = bounds.Top + Math.Max(0, (bounds.Height - _starSize) \ 2)

        Dim rects(_maxStars - 1) As Rectangle
        For i As Integer = 0 To _maxStars - 1
            rects(i) = New Rectangle(startX + i * stepX, top, _starSize, _starSize)
        Next
        Return rects
    End Function

    Private Class RatingRenderer
        Inherits ToolStripProfessionalRenderer
        Private parent As RatingContextMenu

        Public Sub New(owner As RatingContextMenu)
            parent = owner
        End Sub

        ' 選單項目背景繪製對每個項目都會觸發，用它畫星星最可靠
        Protected Overrides Sub OnRenderMenuItemBackground(e As ToolStripItemRenderEventArgs)
            MyBase.OnRenderMenuItemBackground(e)   ' 先畫預設背景/hover 高亮

            If e.Item IsNot parent._ratingItem Then Return

            Dim g As Graphics = e.Graphics
            g.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias

            ' 背景事件的座標原點在項目左上角，因此用 (0,0) 為基準的範圍
            Dim rects As Rectangle() = parent.GetStarRects(New Rectangle(Point.Empty, e.Item.Size))

            For i As Integer = 1 To parent._maxStars
                Dim rect As Rectangle = rects(i - 1)
                Dim fillColor1 As Color = If(i <= parent._selectedRating, Color.FromArgb(255, 220, 100), Color.LightGray)
                Dim fillColor2 As Color = If(i <= parent._selectedRating, Color.FromArgb(255, 140, 0), Color.Gray)

                Using brush As New Drawing2D.LinearGradientBrush(rect, fillColor1, fillColor2, Drawing2D.LinearGradientMode.Vertical)
                    DrawStar(g, brush, rect)
                End Using
            Next
        End Sub

        Private Sub DrawStar(g As Graphics, brush As Brush, rect As Rectangle)
            Dim pts As PointF() = {
                New PointF(rect.Left + rect.Width * 0.5F, rect.Top),
                New PointF(rect.Left + rect.Width * 0.62F, rect.Top + rect.Height * 0.38F),
                New PointF(rect.Right, rect.Top + rect.Height * 0.38F),
                New PointF(rect.Left + rect.Width * 0.68F, rect.Top + rect.Height * 0.62F),
                New PointF(rect.Left + rect.Width * 0.82F, rect.Bottom),
                New PointF(rect.Left + rect.Width * 0.5F, rect.Top + rect.Height * 0.75F),
                New PointF(rect.Left + rect.Width * 0.18F, rect.Bottom),
                New PointF(rect.Left + rect.Width * 0.32F, rect.Top + rect.Height * 0.62F),
                New PointF(rect.Left, rect.Top + rect.Height * 0.38F),
                New PointF(rect.Left + rect.Width * 0.38F, rect.Top + rect.Height * 0.38F)
            }
            g.FillPolygon(brush, pts)
            g.DrawPolygon(Pens.DarkGray, pts)
        End Sub
    End Class

    Protected Overrides Sub OnItemClicked(e As ToolStripItemClickedEventArgs)
        MyBase.OnItemClicked(e)

        If e.ClickedItem IsNot _ratingItem Then Return

        Dim mousePos As Point = Me.PointToClient(Control.MousePosition)
        Dim rects As Rectangle() = GetStarRects(e.ClickedItem.Bounds)

        For i As Integer = 0 To _maxStars - 1
            If rects(i).Contains(mousePos) Then
                UpdateRating(i + 1)
                Exit For
            End If
        Next
    End Sub
End Class

End Namespace
