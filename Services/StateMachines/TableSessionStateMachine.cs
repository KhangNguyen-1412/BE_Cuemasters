using System;
using BilliardsBooking.API.Enums;
using BilliardsBooking.API.Models;

namespace BilliardsBooking.API.Services.StateMachines
{
    public static class TableSessionStateMachine
    {
        public static bool CanTransition(TableSessionStatus from, TableSessionStatus to)
        {
            return (from, to) switch
            {
                (TableSessionStatus.Active, TableSessionStatus.Completed) => true,
                _ => false
            };
        }

        public static void Transition(TableSession session, TableSessionStatus to)
        {
            if (!CanTransition(session.Status, to))
            {
                throw new InvalidOperationException(
                    $"Cannot transition table session from {session.Status} to {to}.");
            }

            session.Status = to;
        }
    }
}
