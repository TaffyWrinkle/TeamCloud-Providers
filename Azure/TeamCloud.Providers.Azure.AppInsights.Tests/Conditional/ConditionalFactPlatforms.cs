﻿/**
 *  Copyright (c) Microsoft Corporation.
 *  Licensed under the MIT License.
 */

using System;

namespace TeamCloud.Providers.Azure.AppInsights.Conditional
{
    [Flags]
    public enum ConditionalFactPlatforms
    {
        FreeBSD = 1,
        Linux = 2,
        OSX = 4,
        Windows = 8
    }
}
