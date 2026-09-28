Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.ComponentModel.Design.Serialization
Imports System.Drawing
Imports System.Globalization

Namespace Global.Aqua

    ''' <summary>Lets the WinForms designer write the items of a list property (ToolBar.Items,
    ''' Buttons.Items) into InitializeComponent as "New Item(text, icon)" -- without it the designer
    ''' can edit the items in the CollectionEditor but has no code to persist them with. Works for any
    ''' item class with a (String, Image) constructor and Text / Icon properties.</summary>
    Friend Class TextIconItemConverter
        Inherits TypeConverter

        Public Overrides Function CanConvertTo(ByVal context As ITypeDescriptorContext, ByVal destinationType As Type) As Boolean
            Return destinationType Is GetType(InstanceDescriptor) OrElse MyBase.CanConvertTo(context, destinationType)
        End Function

        Public Overrides Function ConvertTo(ByVal context As ITypeDescriptorContext, ByVal culture As CultureInfo, ByVal value As Object, ByVal destinationType As Type) As Object
            If destinationType Is GetType(InstanceDescriptor) AndAlso value IsNot Nothing Then
                Dim t As Type = value.GetType()
                Dim ctor As Reflection.ConstructorInfo = t.GetConstructor(New Type() {GetType(String), GetType(Image)})
                If ctor IsNot Nothing Then
                    Dim text As Object = t.GetProperty("Text").GetValue(value, Nothing)
                    Dim icon As Object = t.GetProperty("Icon").GetValue(value, Nothing)
                    Return New InstanceDescriptor(ctor, New Object() {text, icon}, True)
                End If
            End If
            Return MyBase.ConvertTo(context, culture, value, destinationType)
        End Function

    End Class

End Namespace
