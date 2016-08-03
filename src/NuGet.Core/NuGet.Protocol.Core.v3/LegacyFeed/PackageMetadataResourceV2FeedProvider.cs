﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol
{
    public class PackageMetadataResourceV2FeedProvider : ResourceProvider
    {
        public PackageMetadataResourceV2FeedProvider()
            : base(typeof(PackageMetadataResource),
                  nameof(PackageMetadataResourceV2FeedProvider),
                  "PackageMetadataResourceLocalProvider")
        {
        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            PackageMetadataResourceV2Feed resource = null;

            if (await source.GetFeedType(token) == FeedType.HttpV2)
            {
                var serviceDocument = await source.GetResourceAsync<ODataServiceDocumentResourceV2>(token);

                var httpSource = await source.GetResourceAsync<HttpSourceResource>(token);

                var feed = new V2FeedParser(httpSource.HttpSource, serviceDocument.BaseAddress, source.PackageSource);

                resource = new PackageMetadataResourceV2Feed(feed);
            }

            return new Tuple<bool, INuGetResource>(resource != null, resource);
        }
    }
}
