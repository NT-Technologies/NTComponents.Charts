# [1.7.0](https://github.com/NT-Technologies/NTComponents.Charts/compare/v1.6.0...v1.7.0) (2026-03-12)


### Features

* **chart:** improve axis label layout and segmented bar totals ([931d82e](https://github.com/NT-Technologies/NTComponents.Charts/commit/931d82e244c9074eb8af8909099f553a5d73260e))

# [1.6.0](https://github.com/NT-Technologies/NTComponents.Charts/compare/v1.5.0...v1.6.0) (2026-03-11)


### Features

* **chart:** improve font loading and resource management ([1457b21](https://github.com/NT-Technologies/NTComponents.Charts/commit/1457b215abe34608014d202a111a217128b4d7bb))

# [1.5.0](https://github.com/NT-Technologies/NTComponents.Charts/compare/v1.4.0...v1.5.0) (2026-03-10)


### Features

* **charts:** add SkiaSharp native assets for Linux ([3ed0212](https://github.com/NT-Technologies/NTComponents.Charts/commit/3ed021268b92049af51ac42fda119318a09db2ef))

# [1.4.0](https://github.com/NT-Technologies/NTComponents.Charts/compare/v1.3.0...v1.4.0) (2026-03-09)


### Features

* **x-axis:** add integral tick handling for X axis ([0cdf439](https://github.com/NT-Technologies/NTComponents.Charts/commit/0cdf439c8149ebd542081e28d4e7da1a7bad185d))

# [1.3.0](https://github.com/NT-Technologies/NTComponents.Charts/compare/v1.2.0...v1.3.0) (2026-03-09)


### Features

* **bar:** segmented bar legend items & custom tooltips ([28e6f85](https://github.com/NT-Technologies/NTComponents.Charts/commit/28e6f857c7c7e4552b70a5edffcc0c1560f93567))

# [1.2.0](https://github.com/NT-Technologies/NTComponents.Charts/compare/v1.1.0...v1.2.0) (2026-03-06)


### Features

* **chart:** improve axis labels, tooltips, and bar fading ([41307fb](https://github.com/NT-Technologies/NTComponents.Charts/commit/41307fb882c6f9bbfc7379e730f193aa2b95db1a))

# [1.1.0](https://github.com/NT-Technologies/NTComponents.Charts/compare/v1.0.0...v1.1.0) (2026-03-06)


### Features

* **chart:** add JS interop for wheel event handling ([12682fe](https://github.com/NT-Technologies/NTComponents.Charts/commit/12682fe82d648ef03b873544713518100dfc1a95))
* **chart:** add series visibility event and new chart controls ([6c5d37d](https://github.com/NT-Technologies/NTComponents.Charts/commit/6c5d37ddb0ca11c0704ce9a2d8e7a828e3e1d992))
* **chart:** improve axis, tooltip, and date gap handling ([c430ebb](https://github.com/NT-Technologies/NTComponents.Charts/commit/c430ebb2a0f2aef49aa829eca5035dbb1ea3a759))
* **chart:** improve interaction callback handling for series ([cd18fbf](https://github.com/NT-Technologies/NTComponents.Charts/commit/cd18fbfa0311ef6cad67d64eefe295878233bfbd))
* **chart:** improve layout for chart buttons and canvas ([33267dc](https://github.com/NT-Technologies/NTComponents.Charts/commit/33267dc782795fe7e0f224957cde700cabc8dbd3))
* **line-series:** add date group click & zoom support ([f592d67](https://github.com/NT-Technologies/NTComponents.Charts/commit/f592d677d9c002ff086d098700d7e433db98182d))

# 1.0.0 (2026-02-20)


* feat(axis)!: refactor axis management and chart layout ([2d02f1e](https://github.com/NT-Technologies/NTComponents.Charts/commit/2d02f1e382c91ebebdade965c5de56535763f2c7))


### Bug Fixes

* **axis:** prevent axis range cache from overriding view ranges ([f7e1220](https://github.com/NT-Technologies/NTComponents.Charts/commit/f7e12208e9f0e56eb9403972e0318a84ecb17455))
* **chart:** use correct default X axis type for NTChart ([c6ae5a7](https://github.com/NT-Technologies/NTComponents.Charts/commit/c6ae5a74c14f204f7a68f72a40d49b621d8c407a))
* **virtualize:** improve stability and accuracy of NTVirtualize ([b0f1f84](https://github.com/NT-Technologies/NTComponents.Charts/commit/b0f1f840cf7e5023065fca018fb9db3bd592c077))
* **x-axis:** improve tick estimation and series clipping ([de7b105](https://github.com/NT-Technologies/NTComponents.Charts/commit/de7b105d1e1a2993b2612c277aacc6e0e2f47d78))


### Features

* add GitHub workflows for NuGet deployment and semantic release ([88eeff1](https://github.com/NT-Technologies/NTComponents.Charts/commit/88eeff108b9d2791f2f639389ba17a9e6aefdff3))
* **annotation:** add support for chart annotations with customizable properties ([abfc590](https://github.com/NT-Technologies/NTComponents.Charts/commit/abfc5904cb4adc66e3ad432225c76717bb41f091))
* **axes:** refactor and enhance axis measurement logic ([a54eb94](https://github.com/NT-Technologies/NTComponents.Charts/commit/a54eb9498dbeac8a82904c5d2f0cefe14f537bfe))
* **axis:** add font size params and refactor render context ([b1234dc](https://github.com/NT-Technologies/NTComponents.Charts/commit/b1234dcd0254cbb2b1f10885afd3f2256ef5e829))
* **axis:** refactor categorical axis detection ([bcfb596](https://github.com/NT-Technologies/NTComponents.Charts/commit/bcfb596bde5d0da8875680db7c1712407e83e851))
* **bar-chart:** add edge padding for bars and improve label rendering ([9a5ef98](https://github.com/NT-Technologies/NTComponents.Charts/commit/9a5ef98acd6adc771378a1c61fe6dfbc99c3c08a))
* **bar-chart:** enhance horizontal bar handling and axis minimum configuration ([75ca372](https://github.com/NT-Technologies/NTComponents.Charts/commit/75ca372f08bb7fc4b042ddb5952f074a6bad8125))
* **bar-chart:** implement custom easing for bar animation ([545c7f6](https://github.com/NT-Technologies/NTComponents.Charts/commit/545c7f69eafbdd8f0ef163f6e7624dbac905c1cd))
* **chart:** add modular title system with NTTitle/NTTitleOptions ([bd8dccc](https://github.com/NT-Technologies/NTComponents.Charts/commit/bd8dccc1f6920229e9ab7808db02a3b5fcf3ef32))
* **chart:** add render/data caching for improved performance ([c0ba681](https://github.com/NT-Technologies/NTComponents.Charts/commit/c0ba681c59f1f01e7321f376e99dbcb7c95a80ec))
* **chart:** enhance axis tick building and rendering performance ([7286afb](https://github.com/NT-Technologies/NTComponents.Charts/commit/7286afbe040175c374f7edac70b678a9cf6577ad))
* **chart:** enhance legend dragging and floating behavior ([29dba31](https://github.com/NT-Technologies/NTComponents.Charts/commit/29dba3146870b597e9d248cc1a30efc36cac158b))
* **chart:** improve device pixel ratio and theme detection ([ac8e5fe](https://github.com/NT-Technologies/NTComponents.Charts/commit/ac8e5fed6dcce593f46e8f74d81bab3a8cff2556))
* **chart:** major refactor of NTChart<TData> structure ([6043aba](https://github.com/NT-Technologies/NTComponents.Charts/commit/6043abacb8ac626098cb880110c8c81ee5b7331a))
* **chart:** refactor axis rendering and improve performance ([c1ba12d](https://github.com/NT-Technologies/NTComponents.Charts/commit/c1ba12d3886bbebade202e4ac0436f2ba2bbc80e))
* **chart:** validate X value type consistency across series ([fcb3dff](https://github.com/NT-Technologies/NTComponents.Charts/commit/fcb3dff6cfbd2fb454311ae239016a463c57d58a))
* **docs:** add repository guidelines and project structure documentation ([a9f9199](https://github.com/NT-Technologies/NTComponents.Charts/commit/a9f919997a618ef05c0cfde3648773c0d8a04eb7))
* **line-series:** enhance zoom functionality and improve date bucket handling ([ea60856](https://github.com/NT-Technologies/NTComponents.Charts/commit/ea6085670e7712f2b458a8a43fc46820f72317c9))
* **pie-chart:** add explosion on hover and inner radius options for slices ([7f469fc](https://github.com/NT-Technologies/NTComponents.Charts/commit/7f469fc6a3270734968022f36df65c21e1264806))
* **rendering:** add RenderOrdered and ordered renderables ([396ccf1](https://github.com/NT-Technologies/NTComponents.Charts/commit/396ccf1b387222a6975047e7b04f8de8cac01d6c))
* **rendering:** cache paints/fonts for chart performance ([7fb52ea](https://github.com/NT-Technologies/NTComponents.Charts/commit/7fb52eace5431768f04ea0ad13d5396b6dbe581b))
* **series-events:** add hover, click, pan, zoom, and reset view event arguments for series interactions ([b5a8149](https://github.com/NT-Technologies/NTComponents.Charts/commit/b5a8149d2b5acc8f1429ee29636ceb5dd55cf41b))
* **tooltip:** increase tooltip header font size to 14 ([3a7f483](https://github.com/NT-Technologies/NTComponents.Charts/commit/3a7f483284977570f36b3cacb47edc2973557b42))
* **treemap:** add drill transition and visual indicators for drillable nodes ([223a243](https://github.com/NT-Technologies/NTComponents.Charts/commit/223a2435aa46255fcf6bfac979b0c67ddcfa8bb0))
* **treemap:** add nested series support and improve hierarchy management ([61e7ad1](https://github.com/NT-Technologies/NTComponents.Charts/commit/61e7ad10d756ab556cccc31eeb1e49c935e1b322))
* **treemap:** implement drill-down functionality and enhance color selection for nodes ([dad5fb3](https://github.com/NT-Technologies/NTComponents.Charts/commit/dad5fb3519c36f87cc8a5b1762240e4552555c08))
* **y-axis:** add grid line rendering and customizable grid line color ([6d10cf9](https://github.com/NT-Technologies/NTComponents.Charts/commit/6d10cf9c2eb510d3c9d2d8ce72f4b71eb9d8748d))
* **y-axis:** adjust title positioning and padding for improved layout ([5f8f193](https://github.com/NT-Technologies/NTComponents.Charts/commit/5f8f19324ec76dcb6de30cf12583bfc5d7d5dff8))


### BREAKING CHANGES

* Axis registration, access, and rendering APIs have changed; previous axis option setters and axis list methods are removed. Series must reference axes via chart properties.

# Changelog

All notable changes to this project will be documented in this file.
