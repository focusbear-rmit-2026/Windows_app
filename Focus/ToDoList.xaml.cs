using System.Windows.Controls;

namespace Focus_Bear.Focus
{
    /// <summary>
    /// The To-Do List page. It used to host the dashboard's tools-todo-list webview; it now hosts the native
    /// <see cref="ToDoScreens.ToDoScreen"/>, which talks to the same API.
    /// </summary>
    public partial class ToDoList : UserControl
    {
        public ToDoList()
        {
            InitializeComponent();
        }
    }
}
