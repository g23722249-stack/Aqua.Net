Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Windows.Forms

' Port of the VB6 frmMenu popup form: a borderless, topmost, owner-drawn dropdown showing one
' MenuItem's children, with hover highlighting, optional icons/checkmarks, "-" separators, and
' cascading child popups for items that themselves have children. Used by AquaForm's main menu bar
' (and available for reuse by a future standalone AquaMenu component).
'
' VB6 built this from a real control array of Label/Image controls, loaded/positioned per item.
' This owner-draws the whole list into a single Paint instead -- simpler to keep in sync with a
' MenuItem tree that can change between shows, and there's no WinForms Designer involved at all
' since this Form only ever gets created at runtime.
Namespace Global.Aqua

    Friend Class MenuPopupForm
        Inherits Form

        Private Class Row
            Public Property Menu As MenuItem
            Public Property IsSeparator As Boolean
            Public Property Bounds As Rectangle
        End Class

        Private ReadOnly _rows As New List(Of Row)()
        Private _owner As MenuItem
        Private _parentPopup As MenuPopupForm
        Private _childPopup As MenuPopupForm
        Private _hoverRow As Integer = -1
        Private _itemHeight As Integer

        Public Event ItemClicked(item As MenuItem)
        ''' <summary>Raised on the ROOT popup only, when the chain hides itself because it lost
        ''' activation (as opposed to being told to close by AquaForm). AquaForm listens for this
        ''' instead of reacting to its own Deactivate -- showing/activating this popup is itself
        ''' one of the things that deactivates AquaForm, so closing on that would close the menu
        ''' the instant it opens.</summary>
        Public Event AutoClosed(sender As Object, e As EventArgs)

        Public Sub New()
            SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer, True)
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.None
            ShowInTaskbar = False
            StartPosition = FormStartPosition.Manual
            TopMost = True
            BackColor = Color.White
        End Sub

        ''' <summary>Root of the recursive popup chain: closes this popup and every descendant.
        ''' Safe to call more than once, and safe to call on a popup that's already disposed (a
        ''' click on a leaf item disposes the whole chain synchronously, which can race with a
        ''' still-pending deferred close queued by OnDeactivate below).</summary>
        Public Sub CloseAll()
            If IsDisposed Then Return
            CloseChild()
            If Visible Then Hide()
        End Sub

        Private Sub CloseChild()
            If _childPopup IsNot Nothing Then
                If Not _childPopup.IsDisposed Then
                    _childPopup.CloseAll()
                    _childPopup.Dispose()
                End If
                _childPopup = Nothing
            End If
        End Sub

        ''' <summary>Populates and shows this popup for <paramref name="menu"/>'s children, anchored
        ''' at <paramref name="screenPoint"/> (top-left in screen coordinates), flipping to stay on
        ''' screen the same way VB6's ShowMenuWindow did.</summary>
        Public Sub ShowFor(ByVal menu As MenuItem, ByVal screenPoint As Point, ByVal font As Font, Optional ByVal parentPopup As MenuPopupForm = Nothing)
            _owner = menu
            _parentPopup = parentPopup
            Me.Font = font
            BuildRows()
            DoLayout()

            Dim pt As Point = screenPoint
            Dim workArea As Rectangle = Screen.FromPoint(pt).WorkingArea
            If pt.X + Width > workArea.Right Then pt.X = Math.Max(workArea.Left, pt.X - Width)
            If pt.Y + Height > workArea.Bottom Then pt.Y = Math.Max(workArea.Top, pt.Y - Height)
            Location = pt

            Show()
            Activate()
        End Sub

        Private Sub BuildRows()
            _rows.Clear()
            If _owner Is Nothing Then Return
            For i = 0 To _owner.Count - 1
                Dim child As MenuItem = _owner(i)
                If Not child.Visible Then Continue For
                _rows.Add(New Row() With {.Menu = child, .IsSeparator = (child.Text.Trim() = "-")})
            Next
        End Sub

        Private Function AnyIcon() As Boolean
            For Each r In _rows
                If Not r.IsSeparator AndAlso r.Menu.Icon IsNot Nothing Then Return True
            Next
            Return False
        End Function

        ''' <summary>A row with a picture and no text (e.g. 我的評價's star strips): the picture is the whole
        ''' item, drawn at its own shape from the icon column on -- not squeezed into the square icon slot.</summary>
        Private Shared Function IsPictureRow(ByVal r As Row) As Boolean
            Return Not r.IsSeparator AndAlso r.Menu.Icon IsNot Nothing AndAlso r.Menu.Text.Trim().Length = 0
        End Function

        ''' <summary>The size a picture row's picture is drawn at: its own, shrunk (keeping its shape) to
        ''' fit the row height.</summary>
        Private Function PictureSize(ByVal img As Image) As Size
            Dim maxH As Integer = Math.Max(1, _itemHeight - 4)
            If img.Height <= maxH Then Return img.Size
            Return New Size(Math.Max(1, CInt(img.Width * maxH / img.Height)), maxH)
        End Function

        Private Function AnyCheck() As Boolean
            For Each r In _rows
                If Not r.IsSeparator AndAlso r.Menu.CheckStyle <> ItemCheckStyle.None Then Return True
            Next
            Return False
        End Function

        Private Sub DoLayout()
            _itemHeight = Math.Max(MenuResources.DefaultMinHeight, TextHeight() + 4)
            Dim hasIcon As Boolean = AnyIcon()
            Dim hasCheck As Boolean = AnyCheck()

            Dim leftInset As Integer = MenuResources.ItemPadding
            If hasCheck Then leftInset += MenuResources.IconSize + MenuResources.ItemPadding
            If hasIcon Then leftInset += MenuResources.IconSize + MenuResources.ItemPadding

            Dim maxTextWidth As Integer = MenuResources.DefaultMinWidth
            Using g As Graphics = CreateGraphics()
                For Each r In _rows
                    If r.IsSeparator Then Continue For
                    Dim w As Integer = TextRenderer.MeasureText(g, r.Menu.Text, Font).Width
                    If IsPictureRow(r) Then
                        ' the picture starts at the icon column: what it needs past that column
                        w = PictureSize(r.Menu.Icon).Width - If(hasIcon, MenuResources.IconSize + MenuResources.ItemPadding, 0)
                    End If
                    If w > maxTextWidth Then maxTextWidth = w
                Next
            End Using

            Dim rightInset As Integer = MenuResources.ItemPadding + MenuResources.IconSize + MenuResources.ItemPadding   ' room for the submenu arrow
            Dim totalWidth As Integer = leftInset + maxTextWidth + rightInset

            Dim y As Integer = MenuResources.TopBottomPadding
            For i = 0 To _rows.Count - 1
                Dim r As Row = _rows(i)
                If r.IsSeparator Then
                    r.Bounds = New Rectangle(0, y, totalWidth, MenuResources.ItemSeparatorHeight)
                    y += MenuResources.ItemSeparatorHeight
                Else
                    r.Bounds = New Rectangle(0, y, totalWidth, _itemHeight)
                    y += _itemHeight + MenuResources.ItemInterval
                End If
            Next

            Dim totalHeight As Integer = y + MenuResources.TopBottomPadding
            ClientSize = New Size(totalWidth, totalHeight)
        End Sub

        Private Function TextHeight() As Integer
            Using g As Graphics = CreateGraphics()
                Return TextRenderer.MeasureText(g, "中", Font).Height
            End Using
        End Function

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g As Graphics = e.Graphics
            Dim bg As Image = MenuResources.GetBackground()
            If bg IsNot Nothing Then
                Skin.DrawStretch(g, bg, New Rectangle(0, 0, Width, Height), horizontal:=False)
            Else
                g.Clear(Color.White)
            End If

            Dim hasIcon As Boolean = AnyIcon()
            Dim hasCheck As Boolean = AnyCheck()

            For i = 0 To _rows.Count - 1
                Dim r As Row = _rows(i)
                If r.IsSeparator Then
                    Dim sepY As Integer = r.Bounds.Top + r.Bounds.Height \ 2
                    Using p As New Pen(Color.FromArgb(190, 190, 190))
                        g.DrawLine(p, 2, sepY, Width - 3, sepY)
                    End Using
                    Continue For
                End If

                Dim rowRect As Rectangle = r.Bounds
                If i = _hoverRow Then
                    Dim sel As Image = MenuResources.GetSelected()
                    Dim hl As Rectangle = New Rectangle(2, rowRect.Top, Width - 4, rowRect.Height)
                    If sel IsNot Nothing Then
                        Skin.DrawStretch(g, sel, hl, horizontal:=True)
                    Else
                        Using b As New SolidBrush(Color.FromArgb(51, 153, 255))
                            g.FillRectangle(b, hl)
                        End Using
                    End If
                End If

                Dim x As Integer = MenuResources.ItemPadding
                If hasCheck Then
                    Dim chk As Image = CheckMarkResources.GetCheckSurface(r.Menu.CheckStyle, r.Menu.Color, r.Menu.Checked, r.Menu.Enabled)
                    If chk IsNot Nothing Then
                        g.DrawImage(chk, New Rectangle(x, rowRect.Top + (rowRect.Height - MenuResources.IconSize) \ 2, MenuResources.IconSize, MenuResources.IconSize))
                    End If
                    x += MenuResources.IconSize + MenuResources.ItemPadding
                End If
                If IsPictureRow(r) Then
                    Dim pic As Image = If(i = _hoverRow AndAlso r.Menu.SelIcon IsNot Nothing, r.Menu.SelIcon, r.Menu.Icon)
                    Dim sz As Size = PictureSize(pic)
                    g.InterpolationMode = Drawing2D.InterpolationMode.HighQualityBicubic
                    g.DrawImage(pic, New Rectangle(x, rowRect.Top + (rowRect.Height - sz.Height) \ 2, sz.Width, sz.Height))
                    Continue For
                End If
                If hasIcon Then
                    Dim ic As Image = If(i = _hoverRow AndAlso r.Menu.SelIcon IsNot Nothing, r.Menu.SelIcon, r.Menu.Icon)
                    If ic IsNot Nothing Then
                        g.DrawImage(ic, New Rectangle(x, rowRect.Top + (rowRect.Height - MenuResources.IconSize) \ 2, MenuResources.IconSize, MenuResources.IconSize))
                    End If
                    x += MenuResources.IconSize + MenuResources.ItemPadding
                End If

                Dim textColor As Color = If(Not r.Menu.Enabled, Color.FromArgb(150, 150, 150), If(i = _hoverRow, Color.White, ForeColor))
                Dim textRect As New Rectangle(x, rowRect.Top, rowRect.Width - x - MenuResources.ItemPadding - MenuResources.IconSize, rowRect.Height)
                TextRenderer.DrawText(g, r.Menu.Text, Font, textRect, textColor, TextFormatFlags.VerticalCenter Or TextFormatFlags.Left Or TextFormatFlags.EndEllipsis)

                If r.Menu.Count > 0 Then
                    Dim arrow As Image = MenuResources.GetNextLevel(Not r.Menu.Enabled)
                    If arrow IsNot Nothing Then
                        Dim ax As Integer = Width - MenuResources.ItemPadding - MenuResources.IconSize
                        g.DrawImage(arrow, New Rectangle(ax, rowRect.Top + (rowRect.Height - MenuResources.IconSize) \ 2, MenuResources.IconSize, MenuResources.IconSize))
                    End If
                End If
            Next

            Using p As New Pen(Color.FromArgb(160, 160, 160))
                g.DrawRectangle(p, 0, 0, Width - 1, Height - 1)
            End Using
        End Sub

        Private Function RowAt(ByVal p As Point) As Integer
            For i = 0 To _rows.Count - 1
                If Not _rows(i).IsSeparator AndAlso _rows(i).Bounds.Contains(p) Then Return i
            Next
            Return -1
        End Function

        Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
            MyBase.OnMouseMove(e)
            Dim idx As Integer = RowAt(e.Location)
            If idx = _hoverRow Then Return
            _hoverRow = idx
            Invalidate()
            CloseChild()
            If idx >= 0 AndAlso _rows(idx).Menu.Count > 0 AndAlso _rows(idx).Menu.Enabled Then
                OpenChild(idx)
            End If
        End Sub

        Private Sub OpenChild(ByVal rowIndex As Integer)
            Dim r As Row = _rows(rowIndex)
            _childPopup = New MenuPopupForm()
            AddHandler _childPopup.ItemClicked, AddressOf OnChildItemClicked
            Dim anchor As Point = PointToScreen(New Point(Width - 4, r.Bounds.Top))
            _childPopup.ShowFor(r.Menu, anchor, Font, Me)
        End Sub

        Private Sub OnChildItemClicked(item As MenuItem)
            RaiseEvent ItemClicked(item)
        End Sub

        Protected Overrides Sub OnMouseClick(e As MouseEventArgs)
            MyBase.OnMouseClick(e)
            If e.Button <> MouseButtons.Left Then Return
            Dim idx As Integer = RowAt(e.Location)
            If idx < 0 Then Return
            Dim r As Row = _rows(idx)
            If Not r.Menu.Enabled Then Return
            If r.Menu.Count > 0 Then Return   ' has a submenu -- click does nothing itself, hover already opened it
            RaiseEvent ItemClicked(r.Menu)
        End Sub

        Protected Overrides Sub OnDeactivate(e As EventArgs)
            MyBase.OnDeactivate(e)
            ' Give a just-opened child popup a moment to claim activation before treating this as
            ' "focus left the whole menu chain" -- clicking into the child would otherwise deactivate
            ' (and close) its own parent immediately.
            '
            ' By the time this runs, a click on a leaf item may already have closed (and Disposed)
            ' this entire popup chain synchronously -- BeginInvoke only guarantees the delegate runs
            ' on this control's thread, not that the control (or _childPopup/_parentPopup) is still
            ' alive when it does. Every access below is guarded accordingly.
            If Not IsHandleCreated OrElse IsDisposed Then Return
            BeginInvoke(CType(Sub()
                                  If IsDisposed Then Return
                                  If _childPopup IsNot Nothing AndAlso Not _childPopup.IsDisposed AndAlso _childPopup.Visible Then Return
                                  RequestCloseChain()
                              End Sub, MethodInvoker))
        End Sub

        ''' <summary>Escape closes the whole chain, reported the same way as clicking elsewhere.</summary>
        Protected Overrides Function ProcessCmdKey(ByRef msg As Message, keyData As Keys) As Boolean
            If keyData = Keys.Escape Then
                RequestCloseChain()
                Return True
            End If
            Return MyBase.ProcessCmdKey(msg, keyData)
        End Function

        ''' <summary>Walks up to the root of the popup chain (handles any nesting depth) and
        ''' closes the whole thing from there, raising AutoClosed on the root.</summary>
        Private Sub RequestCloseChain()
            Dim root As MenuPopupForm = Me
            While root._parentPopup IsNot Nothing AndAlso Not root._parentPopup.IsDisposed
                root = root._parentPopup
            End While
            If root.IsDisposed Then Return
            root.CloseAll()
            root.NotifyAutoClosed()
        End Sub

        ''' <summary>Friend seam so a descendant popup can raise AutoClosed on the root (RaiseEvent
        ''' only works on Me).</summary>
        Friend Sub NotifyAutoClosed()
            RaiseEvent AutoClosed(Me, EventArgs.Empty)
        End Sub

        Protected Overrides ReadOnly Property CreateParams As CreateParams
            Get
                Const WS_EX_TOOLWINDOW As Integer = &H80
                Dim cp As CreateParams = MyBase.CreateParams
                cp.ExStyle = cp.ExStyle Or WS_EX_TOOLWINDOW   ' no taskbar/alt-tab entry, matches ShowInTaskbar=False intent
                Return cp
            End Get
        End Property

    End Class

End Namespace
