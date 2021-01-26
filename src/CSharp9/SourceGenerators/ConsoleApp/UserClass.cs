using PrintableFields;

namespace ConsoleApp
{
    public partial class UserClass
    {
        [Printable]
        private int _field;
        public void UserMethod()
        {            
            this.Print_field();
        }

        public partial void PrintAllFields();
    }
}
