// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.InMemory.FunctionalTests;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Remotion.Linq;
using Remotion.Linq.Clauses;
using Xunit;

namespace Microsoft.EntityFrameworkCore.Infrastructure.Tests
{
    public class CoreEventIdTest
    {
        [Fact]
        public void Every_eventId_has_a_logger_method_and_logs_when_level_enabled()
        {
            var entityType = new EntityType(typeof(object), new Model(new ConventionSet()), ConfigurationSource.Convention);
            var property = new Property("A", typeof(int), null, null, entityType, ConfigurationSource.Convention, ConfigurationSource.Convention);
            var queryModel = new QueryModel(new MainFromClause("A", typeof(object), Expression.Constant("A")), new SelectClause(Expression.Constant("A")));

            var fakeFactories = new Dictionary<Type, Func<object>>
            {
                { typeof(Type), () => typeof(object) },
                { typeof(QueryModel), () => queryModel },
                { typeof(string), () => "Fake" },
                { typeof(IExpressionPrinter), () => new ExpressionPrinter() },
                { typeof(Expression), () => Expression.Constant("A") },
                { typeof(IEntityType), () => entityType },
                { typeof(IKey), () => new Key(new[] { property }, ConfigurationSource.Convention) },
                { typeof(IServiceProvider), () => new FakeServiceProvider() },
                { typeof(ICollection<IServiceProvider>), () => new List<IServiceProvider>() }
            };

            InMemoryTestHelpers.Instance.TestEventLogging(typeof(CoreEventId), typeof(CoreLoggerExtensions), fakeFactories);
        }

        private class FakeServiceProvider : IServiceProvider
        {
            public object GetService(Type serviceType) => null;
        }
    }
}
