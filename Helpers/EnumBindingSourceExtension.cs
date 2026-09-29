using System;
using System.Windows.Markup;

namespace LumChems.Helpers
{
    public class EnumBindingSourceExtension : MarkupExtension
    {
        private Type? _enumType;

        public Type? EnumType
        {
            get => _enumType;
            set
            {
                if (value != _enumType)
                {
                    if (value != null && !value.IsEnum)
                        throw new ArgumentException("Type must be for an Enum.");
                    _enumType = value;
                }
            }
        }

        public EnumBindingSourceExtension() { }
        public EnumBindingSourceExtension(Type enumType) { EnumType = enumType; }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (_enumType == null)
                throw new InvalidOperationException("The EnumType must be specified.");

            return Enum.GetValues(_enumType);
        }
    }
}