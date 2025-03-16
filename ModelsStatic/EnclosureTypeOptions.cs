namespace HVACLoadTerminals.ModelsStatic
{
    public static class EnclosureTypeOptions
    {
        [Description("Стены, включая стены в грунте")]
        public static string Wall => "Стена";
    
        [Description("Покрытия и перекрытия над проездами")]
        public static string Roof => "Кровля";
    
        [Description("Перекрытия чердачные, над неотапливаемыми подпольями и подвалами, полы по грунту")]
        public static string Floor => "Пол";
    
        [Description("Окна, светопрозрачные фасадные конструкции")]
        public static string Window => "Окно";
    
        [Description("Фонари")]
        public static string Skylight => "Фонарь";
    
        [Description("Двери и витражи")]
        public static string Curtain => "Витраж";
        
        [Description("Двери и витражи")]
        public static string Door => "Дверь";
    }
}
