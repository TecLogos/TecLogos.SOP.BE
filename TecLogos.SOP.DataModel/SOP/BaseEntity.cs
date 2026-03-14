namespace TecLogos.SOP.DataModel.SOP
{
    public abstract class BaseEntity
    {
        public Guid ID { get; set; }
        public int Version { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsDeleted { get; set; } = false;
        public DateTime Created { get; set; }
        public Guid CreatedByID { get; set; }
        public DateTime? Modified { get; set; }
        public Guid? ModifiedByID { get; set; }
        public DateTime? Deleted { get; set; }
        public Guid? DeletedByID { get; set; }
    }
}
