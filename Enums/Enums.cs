namespace BilliardsBooking.API.Enums
{
    public enum Role { Customer, Staff, Admin }
    public enum TableType { Pool, Snooker, Carom }
    public enum TableManualStatus { Available, Maintenance }
    public enum TableRealTimeStatus { Available, Occupied, Reserved, Maintenance }
    public enum BookingStatus { Pending, Confirmed, InProgress, Completed, Cancelled, NoShow }
    public enum BookingType { Online, WalkIn }
    public enum ReservationStatus { Pending, Confirmed, CheckedIn, Completed, Cancelled, NoShow }
    public enum TableSessionStatus { Active, Completed }
    public enum SessionType { Online, WalkIn }
    public enum InvoiceLineType { TableTime, FnB, Coaching, Discount, DepositDeduction }
    public enum PaymentStatus { Pending, Completed, Failed, Refunded }
    public enum PaymentMethod { Cash, VnPay, Stripe }
    public enum PaymentType { Deposit, FinalSettlement, FullPayment, Refund, MembershipPurchase, ForfeitAdjustment }
    public enum MembershipTier { Free, Silver, Gold }
    public enum Specialty { Pool, Snooker, Carom, AllRound }
    public enum FnBCategory { Drinks, Snacks, Combos, MainCourse }
    public enum BenefitType { TableDiscount, PriorityBooking, FreeCoaching }
    public enum QueuedEmailStatus { Pending, Sent, Failed }
    public enum AuditAction
    {
        ReservationCreated,
        ReservationConfirmed,
        ReservationCancelled,
        ReservationNoShow,
        SessionStarted,
        SessionTableChanged,
        SessionFnBAdded,
        SessionCoachLinked,
        SessionCheckedOut,
        TableStatusOverride,
        PaymentRecorded,
        BufferConfigChanged,
        UserRegistered,
        UserLogin,
        UserLoginFailed,
        UserPasswordChanged,
        UserStatusChanged
    }
}
