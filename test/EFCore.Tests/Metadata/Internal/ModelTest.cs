// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Moq;
using Xunit;

namespace Microsoft.EntityFrameworkCore.Tests.Metadata.Internal
{
    public class ModelTest
    {
        [Fact]
        public void Use_of_custom_IModel_throws()
        {
            Assert.Equal(
                CoreStrings.CustomMetadata(nameof(Use_of_custom_IModel_throws), nameof(IModel), "IModelProxy"),
                Assert.Throws<NotSupportedException>(() => Mock.Of<IModel>().AsModel()).Message);
        }

        [Fact]
        public void Snapshot_change_tracking_is_used_by_default()
        {
            Assert.Equal(ChangeTrackingStrategy.Snapshot, new Model().ChangeTrackingStrategy);
            Assert.Equal(ChangeTrackingStrategy.Snapshot, new Model().GetChangeTrackingStrategy());
        }

        [Fact]
        public void Change_tracking_strategy_can_be_changed()
        {
            var model = new Model { ChangeTrackingStrategy = ChangeTrackingStrategy.ChangingAndChangedNotifications };
            Assert.Equal(ChangeTrackingStrategy.ChangingAndChangedNotifications, model.ChangeTrackingStrategy);

            model.ChangeTrackingStrategy = ChangeTrackingStrategy.ChangedNotifications;
            Assert.Equal(ChangeTrackingStrategy.ChangedNotifications, model.GetChangeTrackingStrategy());
        }

        [Fact]
        public void Can_add_and_remove_entity_by_type()
        {
            var model = new Model();
            Assert.Null(model.FindEntityType(typeof(Customer)));
            Assert.Null(model.RemoveEntityType(typeof(Customer)));

            var entityType = model.AddEntityType(typeof(Customer));

            Assert.Equal(typeof(Customer), entityType.ClrType);
            Assert.NotNull(model.FindEntityType(typeof(Customer)));
            Assert.Same(model, entityType.Model);
            Assert.NotNull(entityType.Builder);

            Assert.Same(entityType, model.GetOrAddEntityType(typeof(Customer)));

            Assert.Equal(new[] { entityType }, model.GetEntityTypes().ToArray());

            Assert.Same(entityType, model.RemoveEntityType(entityType.ClrType));

            Assert.Null(model.RemoveEntityType(entityType.ClrType));
            Assert.Null(model.FindEntityType(typeof(Customer)));
            Assert.Null(entityType.Builder);
        }

        [Fact]
        public void Can_add_and_remove_entity_by_name()
        {
            var model = new Model();
            Assert.Null(model.FindEntityType(typeof(Customer).FullName));
            Assert.Null(model.RemoveEntityType(typeof(Customer).FullName));

            var entityType = model.AddEntityType(typeof(Customer).FullName);

            Assert.Null(entityType.ClrType);
            Assert.Equal(typeof(Customer).FullName, entityType.Name);
            Assert.NotNull(model.FindEntityType(typeof(Customer).FullName));
            Assert.Same(model, entityType.Model);
            Assert.NotNull(entityType.Builder);

            Assert.Same(entityType, model.GetOrAddEntityType(typeof(Customer).FullName));

            Assert.Equal(new[] { entityType }, model.GetEntityTypes().ToArray());

            Assert.Same(entityType, model.RemoveEntityType(entityType.Name));

            Assert.Null(model.RemoveEntityType(entityType.Name));
            Assert.Null(model.FindEntityType(typeof(Customer).FullName));
            Assert.Null(entityType.Builder);
        }

        [Fact]
        public void Can_add_entity_types_with_delegated_identity()
        {
            IMutableModel model = new Model();
            var customerType = model.AddEntityType(typeof(Customer));
            var idProperty = customerType.GetOrAddProperty(Customer.IdProperty);
            var customerKey = customerType.AddKey(idProperty);
            var delegatedOrderType = model.AddDelegatedIdentityEntityType(typeof(Order), nameof(Customer.Orders), customerType);

            var fkProperty = delegatedOrderType.AddProperty("ShadowId", typeof(int));
            var orderKey = delegatedOrderType.AddKey(fkProperty);
            var fk = delegatedOrderType.AddForeignKey(fkProperty, customerKey, customerType);
            var index = delegatedOrderType.AddIndex(fkProperty);

            Assert.Same(fkProperty, delegatedOrderType.GetProperties().Single());
            Assert.Same(orderKey, delegatedOrderType.GetKeys().Single());
            Assert.Same(fk, delegatedOrderType.GetForeignKeys().Single());
            Assert.Same(index, delegatedOrderType.GetIndexes().Single());
            Assert.Equal(new[] { customerType, delegatedOrderType }, model.GetEntityTypes());
            Assert.True(model.IsDelegatedIdentityEntityType(typeof(Order)));
            Assert.True(model.IsDelegatedIdentityEntityType(typeof(Order).DisplayName()));
            Assert.Same(delegatedOrderType,
                model.FindDelegatedIdentityEntityType(typeof(Order).DisplayName(), nameof(Customer.Orders), customerType));
            Assert.Same(delegatedOrderType,
                model.FindDelegatedIdentityEntityType(typeof(Order).DisplayName(), nameof(Customer.Orders), (IEntityType)customerType));

            Assert.Equal(CoreStrings.ClashingDelegatedIdentityEntityType(typeof(Order).DisplayName(fullName: false)),
                Assert.Throws<InvalidOperationException>(() => model.AddEntityType(typeof(Order))).Message);
            Assert.Equal(CoreStrings.ClashingNonDelegatedIdentityEntityType(
                nameof(Customer) + "." + nameof(Customer.Orders) + "#"
                + nameof(Order) + "." + nameof(Order.Customer) + "#" + nameof(Customer)),
                Assert.Throws<InvalidOperationException>(() => model.AddDelegatedIdentityEntityType(typeof(Customer), nameof(Order.Customer), delegatedOrderType)).Message);

            Assert.Equal(CoreStrings.ForeignKeySelfReferencingDelegatedIdentity(
                nameof(Customer) + "." + nameof(Customer.Orders) + "#" + nameof(Order)),
                Assert.Throws<InvalidOperationException>(
                    () => delegatedOrderType.AddForeignKey(fkProperty, orderKey, delegatedOrderType)).Message);

            Assert.Equal(CoreStrings.EntityTypeInUseByForeignKey(
                nameof(Customer) + "." + nameof(Customer.Orders) + "#" + nameof(Order),
                nameof(Customer), Property.Format(fk.Properties)),
                Assert.Throws<InvalidOperationException>(() => model.RemoveDelegatedIdentityEntityType(delegatedOrderType)).Message);

            delegatedOrderType.RemoveForeignKey(fk.Properties, fk.PrincipalKey, fk.PrincipalEntityType);

            Assert.Same(delegatedOrderType, model.RemoveDelegatedIdentityEntityType(
                typeof(Order), nameof(Customer.Orders), customerType));
            Assert.Null(((EntityType)delegatedOrderType).Builder);
            Assert.Null(model.RemoveDelegatedIdentityEntityType(delegatedOrderType));
        }

        [Fact]
        public void Cannot_remove_entity_type_when_referenced_by_foreign_key()
        {
            var model = new Model();
            var customerType = model.GetOrAddEntityType(typeof(Customer));
            var idProperty = customerType.GetOrAddProperty(Customer.IdProperty);
            var customerKey = customerType.GetOrAddKey(idProperty);
            var orderType = model.GetOrAddEntityType(typeof(Order));
            var customerFk = orderType.GetOrAddProperty(Order.CustomerIdProperty);

            orderType.AddForeignKey(customerFk, customerKey, customerType);

            Assert.Equal(
                CoreStrings.EntityTypeInUseByReferencingForeignKey(
                    typeof(Customer).Name,
                    "{'" + Order.CustomerIdProperty.Name + "'}",
                    typeof(Order).Name),
                Assert.Throws<InvalidOperationException>(() => model.RemoveEntityType(customerType.Name)).Message);
        }

        [Fact]
        public void Cannot_remove_entity_type_when_it_has_derived_types()
        {
            var model = new Model();
            var customerType = model.GetOrAddEntityType(typeof(Customer));
            var specialCustomerType = model.GetOrAddEntityType(typeof(SpecialCustomer));

            specialCustomerType.HasBaseType(customerType);

            Assert.Equal(
                CoreStrings.EntityTypeInUseByDerived(typeof(Customer).Name, typeof(SpecialCustomer).Name),
                Assert.Throws<InvalidOperationException>(() => model.RemoveEntityType(customerType.Name)).Message);
        }

        [Fact]
        public void Adding_duplicate_entity_by_type_throws()
        {
            var model = new Model();
            Assert.Null(model.RemoveEntityType(typeof(Customer).FullName));

            model.AddEntityType(typeof(Customer));

            Assert.Equal(
                CoreStrings.DuplicateEntityType(nameof(Customer)),
                Assert.Throws<InvalidOperationException>(() => model.AddEntityType(typeof(Customer))).Message);
        }

        [Fact]
        public void Adding_duplicate_entity_by_name_throws()
        {
            var model = new Model();
            Assert.Null(model.RemoveEntityType(typeof(Customer)));

            model.AddEntityType(typeof(Customer));

            Assert.Equal(
                CoreStrings.DuplicateEntityType(typeof(Customer).FullName),
                Assert.Throws<InvalidOperationException>(() => model.AddEntityType(typeof(Customer).FullName)).Message);
        }

        [Fact]
        public void Can_get_entity_by_type()
        {
            var model = new Model();
            var entityType = model.GetOrAddEntityType(typeof(Customer));

            Assert.Same(entityType, model.FindEntityType(typeof(Customer)));
            Assert.Same(entityType, model.FindEntityType(typeof(Customer)));
            Assert.Null(model.FindEntityType(typeof(string)));
        }

        [Fact]
        public void Can_get_entity_by_name()
        {
            var model = new Model();
            var entityType = model.GetOrAddEntityType(typeof(Customer).FullName);

            Assert.Same(entityType, model.FindEntityType(typeof(Customer).FullName));
            Assert.Same(entityType, model.FindEntityType(typeof(Customer).FullName));
            Assert.Null(model.FindEntityType(typeof(string)));
        }

        [Fact]
        public void Entities_are_ordered_by_name()
        {
            var model = new Model();
            var entityType1 = model.AddEntityType(typeof(Order));
            var entityType2 = model.AddEntityType(typeof(Customer));

            Assert.True(new[] { entityType2, entityType1 }.SequenceEqual(model.GetEntityTypes()));
        }

        [Fact]
        public void Can_get_referencing_foreign_keys()
        {
            var model = new Model();
            var entityType1 = model.AddEntityType(typeof(Customer));
            var entityType2 = model.AddEntityType(typeof(Order));
            var keyProperty = entityType1.AddProperty("Id", typeof(int));
            var fkProperty = entityType2.AddProperty("CustomerId", typeof(int));
            var foreignKey = entityType2.GetOrAddForeignKey(fkProperty, entityType1.AddKey(keyProperty), entityType1);

            var referencingForeignKeys = entityType1.GetReferencingForeignKeys();

            Assert.Same(foreignKey, referencingForeignKeys.Single());
            Assert.Same(foreignKey, entityType1.GetReferencingForeignKeys().Single());
        }

        private class Customer
        {
            public static readonly PropertyInfo IdProperty = typeof(Customer).GetProperty("Id");

            public int Id { get; set; }
            public string Name { get; set; }
            public ICollection<Order> Orders { get; set; }
        }

        private class SpecialCustomer : Customer
        {
        }

        private class Order
        {
            public static readonly PropertyInfo CustomerIdProperty = typeof(Order).GetProperty("CustomerId");

            public int Id { get; set; }
            public int CustomerId { get; set; }
            public Customer Customer { get; set; }
        }
    }
}
