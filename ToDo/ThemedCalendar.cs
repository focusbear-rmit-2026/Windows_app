using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace Focus_Bear.ToDoScreens
{
    /// <summary>
    /// The stock WPF <see cref="Calendar"/> in the app's theme: its month, day and year cells redrawn from the
    /// theme's brushes (BackgroundColor, ForegroundColor, TrayCardBg, TrayCardBorder, TraySubtitleColor), the
    /// selected day filled and today ringed in the accent. The stock look paints its own light face, which sat
    /// as a white block in dark mode. The Calendar's behaviour — keyboard, paging, month/year views — is untouched:
    /// only the templates change, keeping every PART_ the CalendarItem looks for.
    /// </summary>
    internal static class ThemedCalendar
    {
        private const string Accent = "#DE7400";

        private static Style _style;

        internal static Calendar Create()
        {
            var calendar = new Calendar();
            calendar.Style = Style;
            return calendar;
        }

        internal static Style Style => _style ?? (_style = (Style)XamlReader.Parse(Xaml));

        private const string NavButton = @"
            <ControlTemplate TargetType='Button'>
              <Border x:Name='Chrome' Background='Transparent' CornerRadius='5' Width='26' Height='24'>
                <ContentPresenter TextElement.FontFamily='Segoe MDL2 Assets' TextElement.FontSize='10'
                                  HorizontalAlignment='Center' VerticalAlignment='Center' TextElement.Foreground='{DynamicResource ForegroundColor}'/>
              </Border>
              <ControlTemplate.Triggers>
                <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Chrome' Property='Background' Value='#22DE7400'/></Trigger>
                <Trigger Property='IsEnabled' Value='False'><Setter TargetName='Chrome' Property='Opacity' Value='0.35'/></Trigger>
              </ControlTemplate.Triggers>
            </ControlTemplate>";

        private static readonly string Xaml = @"
<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
       xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
       TargetType='Calendar'>
  <Setter Property='Foreground' Value='{DynamicResource ForegroundColor}'/>
  <Setter Property='Background' Value='{DynamicResource TrayCardBg}'/>
  <Setter Property='BorderBrush' Value='{DynamicResource TrayCardBorder}'/>
  <Setter Property='BorderThickness' Value='1'/>
  <Setter Property='Template'>
    <Setter.Value>
      <ControlTemplate TargetType='Calendar'>
        <StackPanel x:Name='PART_Root' HorizontalAlignment='Center'>
          <CalendarItem x:Name='PART_CalendarItem' Style='{TemplateBinding CalendarItemStyle}'
                        Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}'
                        BorderThickness='{TemplateBinding BorderThickness}'/>
        </StackPanel>
      </ControlTemplate>
    </Setter.Value>
  </Setter>

  <Setter Property='CalendarItemStyle'>
    <Setter.Value>
      <Style TargetType='CalendarItem'>
        <Setter Property='Template'>
          <Setter.Value>
            <ControlTemplate TargetType='CalendarItem'>
              <ControlTemplate.Resources>
                <DataTemplate x:Key='{x:Static CalendarItem.DayTitleTemplateResourceKey}'>
                  <TextBlock Text='{Binding}' FontSize='10.5' FontWeight='SemiBold' HorizontalAlignment='Center'
                             Margin='0,2,0,4' Foreground='{DynamicResource TraySubtitleColor}'/>
                </DataTemplate>
              </ControlTemplate.Resources>
              <Grid x:Name='PART_Root'>
                <Border Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}'
                        BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='8' Padding='6,4,6,6'>
                  <Grid>
                    <Grid.RowDefinitions>
                      <RowDefinition Height='Auto'/>
                      <RowDefinition Height='*'/>
                    </Grid.RowDefinitions>
                    <Grid.ColumnDefinitions>
                      <ColumnDefinition Width='Auto'/>
                      <ColumnDefinition Width='*'/>
                      <ColumnDefinition Width='Auto'/>
                    </Grid.ColumnDefinitions>
                    <Button x:Name='PART_PreviousButton' Grid.Column='0' Content='&#xE76B;' Focusable='False' Cursor='Hand'>
                      <Button.Template>" + NavButton + @"</Button.Template>
                    </Button>
                    <Button x:Name='PART_HeaderButton' Grid.Column='1' Focusable='False' Cursor='Hand' HorizontalAlignment='Center'>
                      <Button.Template>
                        <ControlTemplate TargetType='Button'>
                          <Border x:Name='Chrome' Background='Transparent' CornerRadius='5' Padding='8,3'>
                            <ContentPresenter TextElement.FontWeight='SemiBold' TextElement.FontSize='12.5'
                                              TextElement.Foreground='{DynamicResource ForegroundColor}' HorizontalAlignment='Center'/>
                          </Border>
                          <ControlTemplate.Triggers>
                            <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Chrome' Property='Background' Value='#22DE7400'/></Trigger>
                          </ControlTemplate.Triggers>
                        </ControlTemplate>
                      </Button.Template>
                    </Button>
                    <Button x:Name='PART_NextButton' Grid.Column='2' Content='&#xE76C;' Focusable='False' Cursor='Hand'>
                      <Button.Template>" + NavButton + @"</Button.Template>
                    </Button>
                    <Grid x:Name='PART_MonthView' Grid.Row='1' Grid.ColumnSpan='3' Margin='0,4,0,0' Visibility='Visible'>
                      <Grid.RowDefinitions>
                        <RowDefinition Height='Auto'/><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/>
                        <RowDefinition Height='Auto'/><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/>
                      </Grid.RowDefinitions>
                      <Grid.ColumnDefinitions>
                        <ColumnDefinition Width='Auto'/><ColumnDefinition Width='Auto'/><ColumnDefinition Width='Auto'/><ColumnDefinition Width='Auto'/>
                        <ColumnDefinition Width='Auto'/><ColumnDefinition Width='Auto'/><ColumnDefinition Width='Auto'/>
                      </Grid.ColumnDefinitions>
                    </Grid>
                    <Grid x:Name='PART_YearView' Grid.Row='1' Grid.ColumnSpan='3' Margin='0,6,0,0' Visibility='Hidden'>
                      <Grid.RowDefinitions>
                        <RowDefinition Height='Auto'/><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/>
                      </Grid.RowDefinitions>
                      <Grid.ColumnDefinitions>
                        <ColumnDefinition Width='Auto'/><ColumnDefinition Width='Auto'/><ColumnDefinition Width='Auto'/><ColumnDefinition Width='Auto'/>
                      </Grid.ColumnDefinitions>
                    </Grid>
                  </Grid>
                </Border>
                <Rectangle x:Name='PART_DisabledVisual' Fill='{DynamicResource BackgroundColor}' Opacity='0.5' RadiusX='8' RadiusY='8' Visibility='Collapsed'/>
              </Grid>
              <ControlTemplate.Triggers>
                <Trigger Property='IsEnabled' Value='False'>
                  <Setter TargetName='PART_DisabledVisual' Property='Visibility' Value='Visible'/>
                </Trigger>
                <DataTrigger Value='Year' Binding='{Binding DisplayMode, RelativeSource={RelativeSource FindAncestor, AncestorType={x:Type Calendar}}}'>
                  <Setter TargetName='PART_MonthView' Property='Visibility' Value='Hidden'/>
                  <Setter TargetName='PART_YearView' Property='Visibility' Value='Visible'/>
                </DataTrigger>
                <DataTrigger Value='Decade' Binding='{Binding DisplayMode, RelativeSource={RelativeSource FindAncestor, AncestorType={x:Type Calendar}}}'>
                  <Setter TargetName='PART_MonthView' Property='Visibility' Value='Hidden'/>
                  <Setter TargetName='PART_YearView' Property='Visibility' Value='Visible'/>
                </DataTrigger>
              </ControlTemplate.Triggers>
            </ControlTemplate>
          </Setter.Value>
        </Setter>
      </Style>
    </Setter.Value>
  </Setter>

  <Setter Property='CalendarDayButtonStyle'>
    <Setter.Value>
      <Style TargetType='CalendarDayButton'>
        <Setter Property='Cursor' Value='Hand'/>
        <Setter Property='FocusVisualStyle' Value='{x:Null}'/>
        <Setter Property='Template'>
          <Setter.Value>
            <ControlTemplate TargetType='CalendarDayButton'>
              <Border x:Name='Cell' Width='30' Height='26' Margin='1' CornerRadius='6' Background='Transparent'
                      BorderBrush='Transparent' BorderThickness='1.5'>
                <ContentPresenter x:Name='Day' HorizontalAlignment='Center' VerticalAlignment='Center'
                                  TextElement.FontSize='12' TextElement.Foreground='{DynamicResource ForegroundColor}'/>
              </Border>
              <ControlTemplate.Triggers>
                <Trigger Property='IsInactive' Value='True'>
                  <Setter TargetName='Day' Property='TextElement.Foreground' Value='{DynamicResource TraySubtitleColor}'/>
                  <Setter TargetName='Day' Property='Opacity' Value='0.6'/>
                </Trigger>
                <Trigger Property='IsMouseOver' Value='True'>
                  <Setter TargetName='Cell' Property='Background' Value='#26DE7400'/>
                </Trigger>
                <Trigger Property='IsHighlighted' Value='True'>
                  <Setter TargetName='Cell' Property='Background' Value='#26DE7400'/>
                </Trigger>
                <Trigger Property='IsToday' Value='True'>
                  <Setter TargetName='Cell' Property='BorderBrush' Value='" + Accent + @"'/>
                  <Setter TargetName='Day' Property='TextElement.Foreground' Value='" + Accent + @"'/>
                  <Setter TargetName='Day' Property='TextElement.FontWeight' Value='SemiBold'/>
                </Trigger>
                <Trigger Property='IsSelected' Value='True'>
                  <Setter TargetName='Cell' Property='Background' Value='" + Accent + @"'/>
                  <Setter TargetName='Day' Property='TextElement.Foreground' Value='White'/>
                  <Setter TargetName='Day' Property='TextElement.FontWeight' Value='SemiBold'/>
                  <Setter TargetName='Day' Property='Opacity' Value='1'/>
                </Trigger>
                <Trigger Property='IsKeyboardFocused' Value='True'>
                  <Setter TargetName='Cell' Property='BorderBrush' Value='{DynamicResource ForegroundColor}'/>
                </Trigger>
                <Trigger Property='IsBlackedOut' Value='True'>
                  <Setter TargetName='Day' Property='Opacity' Value='0.3'/>
                </Trigger>
                <Trigger Property='IsEnabled' Value='False'>
                  <Setter TargetName='Day' Property='Opacity' Value='0.35'/>
                </Trigger>
              </ControlTemplate.Triggers>
            </ControlTemplate>
          </Setter.Value>
        </Setter>
      </Style>
    </Setter.Value>
  </Setter>

  <Setter Property='CalendarButtonStyle'>
    <Setter.Value>
      <Style TargetType='CalendarButton'>
        <Setter Property='Cursor' Value='Hand'/>
        <Setter Property='FocusVisualStyle' Value='{x:Null}'/>
        <Setter Property='Template'>
          <Setter.Value>
            <ControlTemplate TargetType='CalendarButton'>
              <Border x:Name='Cell' Width='52' Height='40' Margin='1' CornerRadius='6' Background='Transparent'
                      BorderBrush='Transparent' BorderThickness='1.5'>
                <ContentPresenter x:Name='Label' HorizontalAlignment='Center' VerticalAlignment='Center'
                                  TextElement.FontSize='12' TextElement.Foreground='{DynamicResource ForegroundColor}'/>
              </Border>
              <ControlTemplate.Triggers>
                <Trigger Property='IsInactive' Value='True'>
                  <Setter TargetName='Label' Property='TextElement.Foreground' Value='{DynamicResource TraySubtitleColor}'/>
                </Trigger>
                <Trigger Property='IsMouseOver' Value='True'>
                  <Setter TargetName='Cell' Property='Background' Value='#26DE7400'/>
                </Trigger>
                <Trigger Property='HasSelectedDays' Value='True'>
                  <Setter TargetName='Cell' Property='BorderBrush' Value='" + Accent + @"'/>
                  <Setter TargetName='Label' Property='TextElement.Foreground' Value='" + Accent + @"'/>
                </Trigger>
                <Trigger Property='IsKeyboardFocused' Value='True'>
                  <Setter TargetName='Cell' Property='BorderBrush' Value='{DynamicResource ForegroundColor}'/>
                </Trigger>
              </ControlTemplate.Triggers>
            </ControlTemplate>
          </Setter.Value>
        </Setter>
      </Style>
    </Setter.Value>
  </Setter>
</Style>";
    }
}
