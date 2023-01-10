namespace QuickAzTables.AspNetIntegrationBasic
{
    public class Car
    {
        public string PlateNo { get; set; } // Row Key
        public string City { get; set; } // Partition Key
    }
}
