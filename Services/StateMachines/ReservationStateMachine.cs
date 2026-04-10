using System;
using BilliardsBooking.API.Enums;
using BilliardsBooking.API.Models;

namespace BilliardsBooking.API.Services.StateMachines
{
    public static class ReservationStateMachine
    {
        public static bool CanTransition(ReservationStatus from, ReservationStatus to)
        {
            return (from, to) switch
            {
                (ReservationStatus.Pending, ReservationStatus.Confirmed) => true,
                (ReservationStatus.Pending, ReservationStatus.Cancelled) => true,
                (ReservationStatus.Confirmed, ReservationStatus.CheckedIn) => true,
                (ReservationStatus.Confirmed, ReservationStatus.Cancelled) => true,
                (ReservationStatus.Confirmed, ReservationStatus.NoShow) => true,
                (ReservationStatus.CheckedIn, ReservationStatus.Completed) => true,
                _ => false
            };
        }

        public static void Transition(Reservation reservation, ReservationStatus to)
        {
            if (!CanTransition(reservation.Status, to))
            {
                throw new InvalidOperationException(
                    $"Cannot transition reservation from {reservation.Status} to {to}.");
            }

            reservation.Status = to;

            if (to == ReservationStatus.Cancelled)
            {
                reservation.CancelledAt = DateTime.UtcNow;
            }

            if (to == ReservationStatus.Confirmed)
            {
                reservation.ConfirmedAt = DateTime.UtcNow;
            }
        }
    }
}
