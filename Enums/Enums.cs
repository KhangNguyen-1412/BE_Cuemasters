namespace BilliardsBooking.API.Enums
{
    public enum Role { Customer, Staff, Admin }
    public enum TableType { Pool, Snooker, Carom }
    public enum TableManualStatus { Available, Maintenance }
    public enum BookingStatus { Pending, Confirmed, InProgress, Completed, Cancelled }
    public enum PaymentStatus { Pending, Completed, Failed, Refunded }
    public enum PaymentMethod { Cash, VnPay, Stripe }
    public enum MembershipTier { Free, Silver, Gold }
    public enum Specialty { Pool, Snooker, Carom, AllRound }
    public enum FnBCategory { Drinks, Snacks, Combos, MainCourse }
    public enum BenefitType { TableDiscount, PriorityBooking, FreeCoaching }
}
