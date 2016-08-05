﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol
{
    public class PackageSearchResourceV2FeedProvider : ResourceProvider
    {
        public PackageSearchResourceV2FeedProvider()
            : base(typeof(PackageSearchResource), nameof(PackageSearchResourceV2FeedProvider), NuGetResourceProviderPositions.Last)
        {
        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source,
                                                                          CancellationToken token)
        {
            PackageSearchResourceV2Feed resource = null;

            if (await source.GetFeedType(token) == FeedType.HttpV2)
            {
                var serviceDocument = await source.GetResourceAsync<ODataServiceDocumentResourceV2>(token);

                var httpSource = await source.GetResourceAsync<HttpSourceResource>(token);

                var feed = new V2FeedParser(httpSource.HttpSource, serviceDocument.BaseAddress, source.PackageSource);

                resource = new PackageSearchResourceV2Feed(feed);
            }

            return new Tuple<bool, INuGetResource>(resource != null, resource);
        }
    }
}
