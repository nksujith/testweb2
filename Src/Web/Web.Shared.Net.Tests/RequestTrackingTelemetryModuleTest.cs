﻿namespace Microsoft.ApplicationInsights.Web
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Web;

    using Microsoft.ApplicationInsights.Channel;
    using Microsoft.ApplicationInsights.Common;
    using Microsoft.ApplicationInsights.Common.Internal;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.ApplicationInsights.Extensibility.Implementation;
    using Microsoft.ApplicationInsights.Extensibility.W3C;
    using Microsoft.ApplicationInsights.TestFramework;
    using Microsoft.ApplicationInsights.Web.Extensibility.Implementation;
    using Microsoft.ApplicationInsights.Web.Helpers;
    using Microsoft.ApplicationInsights.Web.Implementation;
    using Microsoft.ApplicationInsights.Web.TestFramework;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Xunit.Sdk;
    using Assert = Xunit.Assert;
    using MsAssert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

    /// <summary>
    /// Platform independent tests for RequestTrackingTelemetryModule.
    /// </summary>
    [TestClass]
    public partial class RequestTrackingTelemetryModuleTest
    {
        private const string TestInstrumentationKey1 = nameof(TestInstrumentationKey1);
        private const string TestInstrumentationKey2 = nameof(TestInstrumentationKey2);
        private const string TestApplicationId1 = nameof(TestApplicationId1);
        private const string TestApplicationId2 = nameof(TestApplicationId2);
        private readonly ConcurrentQueue<ITelemetry> sentTelemetry = new ConcurrentQueue<ITelemetry>();

        [TestCleanup]
        public void Cleanup()
        {
            while (this.sentTelemetry.TryDequeue(out _))
            {
            }

            while (Activity.Current != null)
            {
                Activity.Current.Stop();
            }
        }

        [TestMethod]
        public void OnBeginRequestDoesNotSetTimeIfItWasAssignedBefore()
        {
            var startTime = DateTimeOffset.UtcNow;

            var context = HttpModuleHelper.GetFakeHttpContext();
            var requestTelemetry = context.CreateRequestTelemetryPrivate();
            requestTelemetry.Timestamp = startTime;

            this.RequestTrackingTelemetryModuleFactory().OnBeginRequest(context);

            Assert.Equal(startTime, requestTelemetry.Timestamp);
        }

        [TestMethod]
        public void OnBeginRequestSetsTimeIfItWasNotAssignedBefore()
        {
            var context = HttpModuleHelper.GetFakeHttpContext();
            var requestTelemetry = context.CreateRequestTelemetryPrivate();
            requestTelemetry.Timestamp = default(DateTimeOffset);

            this.RequestTrackingTelemetryModuleFactory().OnBeginRequest(context);

            Assert.NotEqual(default(DateTimeOffset), requestTelemetry.Timestamp);
        }

        [TestMethod]
        public void RequestIdIsAvailableAfterOnBegin()
        {
            var context = HttpModuleHelper.GetFakeHttpContext();
            var requestTelemetry = context.CreateRequestTelemetryPrivate();

            this.RequestTrackingTelemetryModuleFactory().OnBeginRequest(context);

            Assert.True(!string.IsNullOrEmpty(requestTelemetry.Id));
        }

        [TestMethod]
        public void OnEndSetsDurationToPositiveValue()
        {
            var context = HttpModuleHelper.GetFakeHttpContext();

            var module = this.RequestTrackingTelemetryModuleFactory();
            module.OnBeginRequest(context);
            module.OnEndRequest(context);

            Assert.True(context.GetRequestTelemetry().Duration.TotalMilliseconds >= 0);
        }

        [TestMethod]
        public void OnEndCreatesRequestTelemetryIfBeginWasNotCalled()
        {
            var context = HttpModuleHelper.GetFakeHttpContext();

            this.RequestTrackingTelemetryModuleFactory().OnEndRequest(context);

            Assert.NotNull(context.GetRequestTelemetry());
        }

        [TestMethod]
        public void OnEndSetsDurationToZeroIfBeginWasNotCalled()
        {
            var context = HttpModuleHelper.GetFakeHttpContext();
            this.RequestTrackingTelemetryModuleFactory().OnEndRequest(context);

            Assert.Equal(0, context.GetRequestTelemetry().Duration.Ticks);
        }

        [TestMethod]
        public void OnEndDoesNotOverrideResponseCode()
        {
            var context = HttpModuleHelper.GetFakeHttpContext();
            context.CreateRequestTelemetryPrivate();
            context.Response.StatusCode = 300;

            var module = this.RequestTrackingTelemetryModuleFactory();
            module.OnBeginRequest(context);
            var requestTelemetry = context.GetRequestTelemetry();
            requestTelemetry.ResponseCode = "Test";

            module.OnEndRequest(context);

            Assert.Equal("Test", requestTelemetry.ResponseCode);
        }

        [TestMethod]
        public void OnEndDoesNotOverrideUrl()
        {
            var context = HttpModuleHelper.GetFakeHttpContext();

            var module = this.RequestTrackingTelemetryModuleFactory();
            module.Initialize(TelemetryConfiguration.CreateDefault());
            module.OnBeginRequest(context);

            var requestTelemetry = context.GetRequestTelemetry();
            requestTelemetry.Url = new Uri("http://test/");

            module.OnEndRequest(context);

            Assert.Equal("http://test/", requestTelemetry.Url.OriginalString);
        }

        [TestMethod]
        public void OnEndSetsResponseCode()
        {
            var context = HttpModuleHelper.GetFakeHttpContext();
            context.Response.StatusCode = 401;

            var module = this.RequestTrackingTelemetryModuleFactory();
            module.OnBeginRequest(context);
            module.OnEndRequest(context);

            Assert.Equal("401", context.GetRequestTelemetry().ResponseCode);
        }

        [TestMethod]
        public void OnEndSetsSuccessToFalseFor400()
        {
            var context = HttpModuleHelper.GetFakeHttpContext();
            context.Response.StatusCode = 400;

            var module = this.RequestTrackingTelemetryModuleFactory();
            module.Initialize(TelemetryConfiguration.CreateDefault());
            module.OnBeginRequest(context);
            module.OnEndRequest(context);

            Assert.Equal(false, context.GetRequestTelemetry().Success);
        }

        [TestMethod]
        public void OnEndSetsSuccessToTrueFor401()
        {
            var context = HttpModuleHelper.GetFakeHttpContext();
            context.Response.StatusCode = 401;

            var module = this.RequestTrackingTelemetryModuleFactory();
            module.Initialize(TelemetryConfiguration.CreateDefault());
            module.OnBeginRequest(context);
            module.OnEndRequest(context);

            Assert.Equal(true, context.GetRequestTelemetry().Success);
        }

        [TestMethod]
        public void OnEndSetsSuccessToTrueFor200()
        {
            var context = HttpModuleHelper.GetFakeHttpContext();
            context.Response.StatusCode = 200;

            var module = this.RequestTrackingTelemetryModuleFactory();
            module.Initialize(TelemetryConfiguration.CreateDefault());
            module.OnBeginRequest(context);
            module.OnEndRequest(context);

            Assert.Equal(true, context.GetRequestTelemetry().Success);
        }

        [TestMethod]
        public void OnEndSetsUrl()
        {
            var context = HttpModuleHelper.GetFakeHttpContext();

            var module = this.RequestTrackingTelemetryModuleFactory();
            module.OnBeginRequest(context);
            module.OnEndRequest(context);

            Assert.Equal(context.Request.Url, context.GetRequestTelemetry().Url);
        }

        [TestMethod]
        public void OnEndDoesNotSetUrlIfDisableTrackingPropertiesIsSet()
        {
            var context = HttpModuleHelper.GetFakeHttpContext();

            var module = this.RequestTrackingTelemetryModuleFactory();
            module.DisableTrackingProperties = true;
            module.OnBeginRequest(context);
            module.OnEndRequest(context);

            Assert.Null(context.GetRequestTelemetry().Url); // "RequestTrackingTelemetryModule should not set Url if DisableTrackingProperties=true"
        }

        [TestMethod]
        public void OnEndTracksRequest()
        {
            var context = HttpModuleHelper.GetFakeHttpContext();

            var sendItems = new List<ITelemetry>();
            var stubTelemetryChannel = new StubTelemetryChannel { OnSend = item => sendItems.Add(item) };
            var configuration = new TelemetryConfiguration
            {
                InstrumentationKey = Guid.NewGuid().ToString(),
                TelemetryChannel = stubTelemetryChannel
            };

            var module = this.RequestTrackingTelemetryModuleFactory(configuration);
            module.OnBeginRequest(context);
            module.OnEndRequest(context);

            Assert.Equal(1, sendItems.Count);
        }

        [TestMethod]
        public void NeedProcessRequestReturnsFalseForDefaultHandler()
        {
            var context = HttpModuleHelper.GetFakeHttpContext();
            context.Response.StatusCode = 200;
            context.Handler = new System.Web.Handlers.AssemblyResourceLoader();

            var requestTelemetry = context.CreateRequestTelemetryPrivate();
            requestTelemetry.Start();

            var module = this.RequestTrackingTelemetryModuleFactory();
            module.Handlers.Add("System.Web.Handlers.AssemblyResourceLoader");

            Assert.False(module.NeedProcessRequest(context));
        }

        [TestMethod]
        public void NeedProcessRequestReturnsTrueForUnknownHandler()
        {
            var context = HttpModuleHelper.GetFakeHttpContext();
            context.Response.StatusCode = 200;
            context.Handler = new FakeHttpHandler();

            var requestTelemetry = context.CreateRequestTelemetryPrivate();
            requestTelemetry.Start();

            var module = this.RequestTrackingTelemetryModuleFactory();

            Assert.True(module.NeedProcessRequest(context));
        }

        [TestMethod]
        public void NeedProcessRequestReturnsFalseForCustomHandler()
        {
            var context = HttpModuleHelper.GetFakeHttpContext();
            context.Response.StatusCode = 200;
            context.Handler = new FakeHttpHandler();

            var requestTelemetry = context.CreateRequestTelemetryPrivate();
            requestTelemetry.Start();

            var module = this.RequestTrackingTelemetryModuleFactory();
            module.Handlers.Add("Microsoft.ApplicationInsights.Web.RequestTrackingTelemetryModuleTest+FakeHttpHandler");

            Assert.False(module.NeedProcessRequest(context));
        }

        [TestMethod]
        public void NeedProcessRequestReturnsTrueForNon200()
        {
            var context = HttpModuleHelper.GetFakeHttpContext();
            context.Response.StatusCode = 500;
            context.Handler = new System.Web.Handlers.AssemblyResourceLoader();

            var requestTelemetry = context.CreateRequestTelemetryPrivate();
            requestTelemetry.Start();

            var module = this.RequestTrackingTelemetryModuleFactory();

            Assert.True(module.NeedProcessRequest(context));
        }

        [TestMethod]
        public void NeedProcessRequestReturnsFalseOnNullHttpContext()
        {
            var module = this.RequestTrackingTelemetryModuleFactory();
            {
                Assert.False(module.NeedProcessRequest(null));
            }
        }

        [TestMethod]
        public void SdkVersionHasCorrectFormat()
        {
            string expectedVersion = SdkVersionHelper.GetExpectedSdkVersion(typeof(RequestTrackingTelemetryModule), prefix: "web:");

            var context = HttpModuleHelper.GetFakeHttpContext();

            var module = this.RequestTrackingTelemetryModuleFactory();
            module.OnBeginRequest(context);
            module.OnEndRequest(context);

            Assert.Equal(expectedVersion, context.GetRequestTelemetry().Context.GetInternalContext().SdkVersion);
        }       

        [TestMethod]
        public void OnEndDoesNotAddSourceFieldForRequestForSameComponent()
        {
            // ARRANGE
            Dictionary<string, string> headers = new Dictionary<string, string>();
            headers.Add(RequestResponseHeaders.RequestContextHeader, TestApplicationId2);

            var context = HttpModuleHelper.GetFakeHttpContext(headers);

            var config = this.CreateDefaultConfig(context, instrumentationKey: TestInstrumentationKey1);
            config.ApplicationIdProvider = new MockApplicationIdProvider(TestInstrumentationKey1, TestApplicationId1);
            var module = this.RequestTrackingTelemetryModuleFactory(config);

            // ACT
            module.OnBeginRequest(context);
            module.OnEndRequest(context);

            // VALIDATE
            Assert.True(string.IsNullOrEmpty(context.GetRequestTelemetry().Source), "RequestTrackingTelemetryModule should not set source for same ikey as itself.");
        }

        [TestMethod]
        public void OnEndAddsSourceFieldForRequestWithCorrelationId()
        {
            // ARRANGE  
            Dictionary<string, string> headers = new Dictionary<string, string>();
            headers.Add(RequestResponseHeaders.RequestContextHeader, this.GetCorrelationIdHeaderValue(TestApplicationId2));

            var context = HttpModuleHelper.GetFakeHttpContext(headers);

            // My instrumentation key and hence app id is random / newly generated. The appId header is different - hence a different component.
            var config = TelemetryConfiguration.CreateDefault();
            config.InstrumentationKey = TestInstrumentationKey1;
            config.ApplicationIdProvider = new MockApplicationIdProvider(TestInstrumentationKey1, TestApplicationId1);
            
            var module = this.RequestTrackingTelemetryModuleFactory(null /*use default*/);
            
            // ACT
            module.Initialize(config);
            module.OnBeginRequest(context);
            module.OnEndRequest(context);

            // VALIDATE
            Assert.Equal(TestApplicationId2, context.GetRequestTelemetry().Source);
        }

        [TestMethod]
        public void OnEndDoesNotAddSourceFieldIfDisableTrackingPropertiesIsSet()
        {
            // ARRANGE  
            Dictionary<string, string> headers = new Dictionary<string, string>();
            headers.Add(RequestResponseHeaders.RequestContextHeader, this.GetCorrelationIdHeaderValue(TestApplicationId2));

            var context = HttpModuleHelper.GetFakeHttpContext(headers);

            // My instrumentation key and hence app id is random / newly generated. The appId header is different - hence a different component.
            var config = TelemetryConfiguration.CreateDefault();
            config.InstrumentationKey = TestInstrumentationKey1;
            config.ApplicationIdProvider = new MockApplicationIdProvider(TestInstrumentationKey1, TestApplicationId1);
            config.ExperimentalFeatures.Add("DeferRequestTrackingProperties");
            
            var module = this.RequestTrackingTelemetryModuleFactory(null /*use default*/);
            
            // ACT
            module.Initialize(config);
            module.OnBeginRequest(context);
            module.OnEndRequest(context);

            // VALIDATE
            Assert.True(string.IsNullOrEmpty(context.GetRequestTelemetry().Source), "RequestTrackingTelemetryModule should not set source if DisableTrackingProperties=true");
        }

        [TestMethod]
        public void OnEndDoesNotAddSourceFieldForRequestWithOutSourceIkeyHeader()
        {
            // ARRANGE                                   
            // do not add any sourceikey header.
            Dictionary<string, string> headers = new Dictionary<string, string>();

            var context = HttpModuleHelper.GetFakeHttpContext(headers);

            var module = this.RequestTrackingTelemetryModuleFactory();
            var config = TelemetryConfiguration.CreateDefault();
            config.InstrumentationKey = Guid.NewGuid().ToString();

            // ACT
            module.Initialize(config);
            module.OnBeginRequest(context);
            module.OnEndRequest(context);

            // VALIDATE
            Assert.True(string.IsNullOrEmpty(context.GetRequestTelemetry().Source), "RequestTrackingTelemetryModule should not set source if not sourceikey found in header");
        }

        [TestMethod]
        public void OnEndDoesNotOverrideSourceField()
        {
            // ARRANGE       
            Dictionary<string, string> headers = new Dictionary<string, string>();
            headers.Add(RequestResponseHeaders.RequestContextHeader, TestApplicationId1);

            var context = HttpModuleHelper.GetFakeHttpContext(headers);

            var module = this.RequestTrackingTelemetryModuleFactory();
            module.OnBeginRequest(context);
            context.GetRequestTelemetry().Source = TestApplicationId2;

            // ACT
            module.OnEndRequest(context);

            // VALIDATE
            Assert.Equal(TestApplicationId2, context.GetRequestTelemetry().Source);
        }

        [TestMethod]
        public void TrackIntermediateRequestSetsProperties()
        {
            string requestId = "|standard-id.";
            var context = HttpModuleHelper.GetFakeHttpContext(new Dictionary<string, string>
            {
                ["Request-Id"] = requestId
            });

            var module = this.RequestTrackingTelemetryModuleFactory(this.CreateDefaultConfig(context));
            module.OnBeginRequest(context);

            var originalRequest = context.GetRequestTelemetry();
            originalRequest.Start(Stopwatch.GetTimestamp() - (1 * Stopwatch.Frequency));

            var restoredActivity = new Activity("dummy").SetParentId(originalRequest.Id).Start();

            module.TrackIntermediateRequest(context, restoredActivity);
            module.OnEndRequest(context);
            Assert.Equal(2, this.sentTelemetry.Count);

            var intermediateRequest = this.sentTelemetry.OfType<RequestTelemetry>().First();

            Assert.Equal(originalRequest.Id, intermediateRequest.Context.Operation.ParentId);
            Assert.Equal(originalRequest.Context.Operation.Id, intermediateRequest.Context.Operation.Id);
            Assert.Equal(restoredActivity.StartTimeUtc, intermediateRequest.Timestamp);
            Assert.Equal(restoredActivity.Duration, intermediateRequest.Duration);
            Assert.True(intermediateRequest.Properties.ContainsKey("AI internal"));
        }

        [TestMethod]
        public void VerifyBehaviorWhenDeferredIsFalse()
        {
            var config = this.CreateDefaultConfig(HttpModuleHelper.GetFakeHttpContext());
            var context = HttpModuleHelper.GetFakeHttpContext();

            // Create, Initialize, and Validate RequestTrackingTelemetryModule
            var module = this.RequestTrackingTelemetryModuleFactory(config);
            MsAssert.IsFalse(module.DisableTrackingProperties, $"{nameof(module.DisableTrackingProperties)} should be False by default.");

            // Validate Telemetry Processor Chain
            MsAssert.IsFalse(config.DefaultTelemetrySink.TelemetryProcessors.Any(x => x is PostSamplingTelemetryProcessor));

            // Run test to generate requestTelemetry
            module.OnBeginRequest(context);
            module.OnEndRequest(context);
            var requestTelemetry = context.GetRequestTelemetry();

            // Validate requestTelemetry
            MsAssert.IsNotNull(requestTelemetry, "TEST ERROR: Failed to create requestTelemetry.");
            MsAssert.IsNotNull(requestTelemetry.Url);
        }

        [TestMethod]
        public void VerifyBehaviorWhenDeferredIsTrue()
        {
            var context = HttpModuleHelper.GetFakeHttpContext();
            var config = new TelemetryConfiguration();
            config.ExperimentalFeatures.Add(ExperimentalConstants.DeferRequestTrackingProperties);

            // Create and Validate RequestTrackingTelemetryModule
            var module = this.RequestTrackingTelemetryModuleFactory(config);
            MsAssert.IsTrue(module.DisableTrackingProperties, "TEST ERROR: Module was not initialized with custom config.");

            // Validate Telemetry Processor Chain
            MsAssert.IsTrue(config.DefaultTelemetrySink.TelemetryProcessors.Any(x => x is PostSamplingTelemetryProcessor), "Module should inject PostSamplingTelemetryProcessor");

            // Run test to generate requestTelemetry
            module.OnBeginRequest(context);
            module.OnEndRequest(context);
            var requestTelemetry = context.GetRequestTelemetry();

            // Validate requestTelemetry
            MsAssert.IsNotNull(requestTelemetry, "TEST ERROR: Failed to create requestTelemetry.");
            MsAssert.IsNotNull(requestTelemetry.Url); // set by PostSamplingTelemetryProcessor
        }

        private TelemetryConfiguration CreateDefaultConfig(HttpContext fakeContext, string rootIdHeaderName = null, string parentIdHeaderName = null, string instrumentationKey = null)
        {
            var telemetryChannel = new StubTelemetryChannel()
            {
                EndpointAddress = "https://endpointaddress",
                OnSend = item => this.sentTelemetry.Enqueue(item)
            };

            var configuration = new TelemetryConfiguration
            {
                TelemetryChannel = telemetryChannel,
                InstrumentationKey = TestInstrumentationKey1,
                ApplicationIdProvider = new MockApplicationIdProvider(TestInstrumentationKey1, TestApplicationId1)
            };
            configuration.TelemetryInitializers.Add(new Microsoft.ApplicationInsights.Extensibility.OperationCorrelationTelemetryInitializer());

            var telemetryInitializer = new TestableOperationCorrelationTelemetryInitializer(fakeContext);

            if (rootIdHeaderName != null)
            {
                telemetryInitializer.RootOperationIdHeaderName = rootIdHeaderName;
            }

            if (parentIdHeaderName != null)
            {
                telemetryInitializer.ParentOperationIdHeaderName = parentIdHeaderName;
            }

            configuration.TelemetryInitializers.Add(telemetryInitializer);

            return configuration;
        }

        private string GetActivityRootId(string telemetryId)
        {
            return telemetryId.Substring(1, telemetryId.IndexOf('.') - 1);
        }

        private RequestTrackingTelemetryModule RequestTrackingTelemetryModuleFactory(TelemetryConfiguration config = null, bool enableW3CTracing = false)
        {
            var module = new RequestTrackingTelemetryModule()
            {
                EnableChildRequestTrackingSuppression = false,
                EnableW3CHeadersExtraction = enableW3CTracing
            };

            if (config == null)
            {
                config = this.CreateDefaultConfig(HttpModuleHelper.GetFakeHttpContext());
            }

            if (enableW3CTracing)
            {
                config.TelemetryInitializers.Add(new W3COperationCorrelationTelemetryInitializer());
            }

            module.Initialize(config);

            return module;
        }

        private string GetCorrelationIdHeaderValue(string applicationId)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}={1}", RequestResponseHeaders.RequestContextCorrelationSourceKey, applicationId);
        }

        internal class FakeHttpHandler : IHttpHandler
        {
            bool IHttpHandler.IsReusable
            {
                get { return false; }
            }

            public void ProcessRequest(System.Web.HttpContext context)
            {
            }
        }

        private class TestableOperationCorrelationTelemetryInitializer : OperationCorrelationTelemetryInitializer
        {
            private readonly HttpContext fakeContext;

            public TestableOperationCorrelationTelemetryInitializer(HttpContext fakeContext)
            {
                this.fakeContext = fakeContext;
            }

            public HttpContext FakeContext
            {
                get { return this.fakeContext; }
            }

            protected override HttpContext ResolvePlatformContext()
            {
                return this.fakeContext;
            }
        }
    }
}