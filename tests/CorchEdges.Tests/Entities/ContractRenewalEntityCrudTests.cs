using CorchEdges.Data;
using CorchEdges.Data.Entities;
using CorchEdges.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CorchEdges.Tests.Entities;

[Trait("Category", TestCategories.Entity)]
[Trait("Entity", "ContractRenewal")]
public class ContractRenewalCrudTests : EntityCrudTestBase<ContractRenewal>
{
    protected override ContractRenewal CreateValidEntity()
    {
        return new ContractRenewal
        {
            ContractId = "RENEWAL_001",
            PropertyNo = 100,
            RoomNo = 201,
            ContractorNo = 1001,
            PropertyName = "Test Apartment Complex",
            ContractorName = "Yamada Taro",
            ProgressStatus = "更新手続き中",
            RenewalDate = DateOnly.FromDateTime(DateTime.Today),
            PreviousContractStartDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-730)),
            PreviousContractEndDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-1)),
            NextContractStartDate = DateOnly.FromDateTime(DateTime.Today),
            NextContractEndDate = DateOnly.FromDateTime(DateTime.Today.AddDays(730)),
            ContractRenewalDate = DateOnly.FromDateTime(DateTime.Today),
            OutputDateTime = DateTime.Now
        };
    }

    protected override void ModifyEntity(ContractRenewal entity)
    {
        entity.ProgressStatus = "更新完了";
        entity.ContractorName = "Yamada Hanako";
    }

    protected override DbSet<ContractRenewal> GetDbSet(EdgesDbContext context)
    {
        return context.ContractRenewals;
    }

    protected override void AssertEntitiesEqual(ContractRenewal expected, ContractRenewal actual)
    {
        base.AssertEntitiesEqual(expected, actual);
        
        Assert.Equal(expected.ContractId, actual.ContractId);
        Assert.Equal(expected.PropertyNo, actual.PropertyNo);
        Assert.Equal(expected.RoomNo, actual.RoomNo);
        Assert.Equal(expected.ContractorNo, actual.ContractorNo);
        Assert.Equal(expected.PropertyName, actual.PropertyName);
        Assert.Equal(expected.ContractorName, actual.ContractorName);
        Assert.Equal(expected.ProgressStatus, actual.ProgressStatus);
        Assert.Equal(expected.PreviousContractStartDate, actual.PreviousContractStartDate);
        Assert.Equal(expected.PreviousContractEndDate, actual.PreviousContractEndDate);
        Assert.Equal(expected.NextContractStartDate, actual.NextContractStartDate);
        Assert.Equal(expected.NextContractEndDate, actual.NextContractEndDate);
    }
}