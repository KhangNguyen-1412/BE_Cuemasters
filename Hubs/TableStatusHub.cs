using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace BilliardsBooking.API.Hubs
{
    public class TableStatusHub : Hub
    {
        // Clients can join groups based on the date they are looking at
        // GroupName: "floorplan-{date}" e.g. "floorplan-2024-05-12"
        public async Task JoinFloorPlanGroup(string dateGroup)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, dateGroup);
        }

        public async Task LeaveFloorPlanGroup(string dateGroup)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, dateGroup);
        }

        // GroupName: "table-{id}-{date}"
        public async Task JoinTableSlotGroup(int tableId, string date)
        {
            var groupName = $"table-{tableId}-{date}";
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        }

        public async Task LeaveTableSlotGroup(int tableId, string date)
        {
            var groupName = $"table-{tableId}-{date}";
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        }
    }
}
