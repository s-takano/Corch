using CorchEdges.Data;
using CorchEdges.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CorchEdges.Tests.Entities;

[Trait("Category", "EntityCrud")]
[Trait("Entity", "ContractTermination")]
public class ContractTerminationCrudTests : EntityCrudTestBase<ContractTermination>
{
    protected override ContractTermination CreateValidEntity()
    {
        return new ContractTermination
        {
            ContractId = "TERMINATION_001",
            PropertyNo = 100,
            RoomNo = 201,
            ContractorNo = 1001,
            PropertyName = "Test Apartment Complex",
            ContractorName = "Sato Taro",
            RoomType = "1LDK",
            ProgressStatus = "解約手続き中",
            ApplicationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-30)),
            NotificationReceiptDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-25)),
            ScheduledMoveOutDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30)),
            ActualMoveOutDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30)),
            MoveOutInspectionDate = DateOnly.FromDateTime(DateTime.Today.AddDays(31)),
            MoveOutInspector = "Inspector Tanaka",
            SettlementCreationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(35)),
            SettlementCreator = "Admin Yamada",
            OutputDateTime = DateTime.Now 
        };
    }

    protected override void ModifyEntity(ContractTermination entity)
    {
        entity.ProgressStatus = "解約完了";
        entity.MoveOutInspector = "Inspector Suzuki";
        entity.SettlementCreator = "Admin Sato";
        entity.FinalApprovalDate = DateOnly.FromDateTime(DateTime.Today.AddDays(40));
    }

    protected override DbSet<ContractTermination> GetDbSet(EdgesDbContext context)
    {
        return context.ContractTerminations;
    }

    protected override void AssertEntitiesEqual(ContractTermination expected, ContractTermination actual)
    {
        base.AssertEntitiesEqual(expected, actual);
        
        Assert.Equal(expected.ContractId, actual.ContractId);
        Assert.Equal(expected.PropertyNo, actual.PropertyNo);
        Assert.Equal(expected.RoomNo, actual.RoomNo);
        Assert.Equal(expected.ContractorNo, actual.ContractorNo);
        Assert.Equal(expected.PropertyName, actual.PropertyName);
        Assert.Equal(expected.ContractorName, actual.ContractorName);
        Assert.Equal(expected.RoomType, actual.RoomType);
        Assert.Equal(expected.ProgressStatus, actual.ProgressStatus);
        Assert.Equal(expected.ApplicationDate, actual.ApplicationDate);
        Assert.Equal(expected.ScheduledMoveOutDate, actual.ScheduledMoveOutDate);
        Assert.Equal(expected.MoveOutInspector, actual.MoveOutInspector);
        Assert.Equal(expected.SettlementCreator, actual.SettlementCreator);
    }
}