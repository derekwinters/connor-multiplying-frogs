# Changelog

## [0.4.0](https://github.com/derekwinters/connor-multiplying-frogs/compare/v0.3.0...v0.4.0) (2026-08-13)


### Features

* **setup:** let a player set their name, defaulting to the frog's colour ([#318](https://github.com/derekwinters/connor-multiplying-frogs/issues/318)) ([a0b871e](https://github.com/derekwinters/connor-multiplying-frogs/commit/a0b871e1d7ef44714f8810c8ed00d264bc11e07b))


### Bug Fixes

* **game-board:** the top and bottom bars reach the screen's edges ([#313](https://github.com/derekwinters/connor-multiplying-frogs/issues/313)) ([1ac85d3](https://github.com/derekwinters/connor-multiplying-frogs/commit/1ac85d3f40fe8f9c4ca00a25ab4940128c1be34e))
* **working-out-grid:** typing an answer enters it in reading order ([#317](https://github.com/derekwinters/connor-multiplying-frogs/issues/317)) ([05cf39e](https://github.com/derekwinters/connor-multiplying-frogs/commit/05cf39e9c7b07d60c7abe407a55ce87632b98cb0))
* **working-out-grid:** you can see which box the next digit goes in ([#316](https://github.com/derekwinters/connor-multiplying-frogs/issues/316)) ([99f1700](https://github.com/derekwinters/connor-multiplying-frogs/commit/99f1700a10f737118d60bd61fe20270ac93a9c28))

## [0.3.0](https://github.com/derekwinters/connor-multiplying-frogs/compare/v0.2.1...v0.3.0) (2026-08-12)


### Features

* **game-board:** one Start log and one End log for the whole pond ([#309](https://github.com/derekwinters/connor-multiplying-frogs/issues/309)) ([d219e57](https://github.com/derekwinters/connor-multiplying-frogs/commit/d219e5779b8e8315da44c63ff7f711c3d410726a)), closes [#296](https://github.com/derekwinters/connor-multiplying-frogs/issues/296)
* **ui:** the pond reads as water — blue water, brown logs, green lily pads ([#302](https://github.com/derekwinters/connor-multiplying-frogs/issues/302)) ([364053a](https://github.com/derekwinters/connor-multiplying-frogs/commit/364053ad7489cb82888cfabf04e5ab00cc6aa752))


### Bug Fixes

* **gatekeeper:** let a gate refusal explain itself instead of crashing ([#307](https://github.com/derekwinters/connor-multiplying-frogs/issues/307)) ([de00c3b](https://github.com/derekwinters/connor-multiplying-frogs/commit/de00c3b9f5d4e089776d1c0735c512197cb544a4)), closes [#196](https://github.com/derekwinters/connor-multiplying-frogs/issues/196)
* **pipeline:** show every issue that needs attention, not only the focus milestone's ([#293](https://github.com/derekwinters/connor-multiplying-frogs/issues/293)) ([04bda5a](https://github.com/derekwinters/connor-multiplying-frogs/commit/04bda5ac23aa1a6e49067247ac7e4704fd460b76))
* **pipeline:** stop the auto-revisit sweep re-waking an issue forever ([#306](https://github.com/derekwinters/connor-multiplying-frogs/issues/306)) ([6f52f5a](https://github.com/derekwinters/connor-multiplying-frogs/commit/6f52f5a8e323cc111f99b29b8af18a3928ec3fe7))
* **ui:** paint the background to the edge of the screen ([#300](https://github.com/derekwinters/connor-multiplying-frogs/issues/300)) ([142949a](https://github.com/derekwinters/connor-multiplying-frogs/commit/142949a771f69527cee510d45488747dbebc3d9e))
* **working-out-grid:** let the keypad and the cells take a tap ([#295](https://github.com/derekwinters/connor-multiplying-frogs/issues/295)) ([f20a1bd](https://github.com/derekwinters/connor-multiplying-frogs/commit/f20a1bd27c96a2b7e530ff7fb16cd3ccf1496a9c))

## [0.2.1](https://github.com/derekwinters/connor-multiplying-frogs/compare/v0.2.0...v0.2.1) (2026-08-11)


### Bug Fixes

* **app:** build and show the game at runtime, so the app is not a blank screen ([#286](https://github.com/derekwinters/connor-multiplying-frogs/issues/286)) ([cca5782](https://github.com/derekwinters/connor-multiplying-frogs/commit/cca5782656817a1973d8d330256a8ef04fbcfb63))
* **build:** the emulator profile builds x86_64 with IL2CPP ([#283](https://github.com/derekwinters/connor-multiplying-frogs/issues/283)) ([d78c97b](https://github.com/derekwinters/connor-multiplying-frogs/commit/d78c97bb0ea8262307514b8df650be7d2957b9a8))

## [0.2.0](https://github.com/derekwinters/connor-multiplying-frogs/compare/v0.1.1...v0.2.0) (2026-08-10)


### Features

* **build:** create the Game scene, the scene the app actually boots into ([#266](https://github.com/derekwinters/connor-multiplying-frogs/issues/266)) ([0e36b1a](https://github.com/derekwinters/connor-multiplying-frogs/commit/0e36b1a8fbf6cfc3847517edbbd59d1ced91b325))
* **core:** add a seeded, deterministic random number generator ([#258](https://github.com/derekwinters/connor-multiplying-frogs/issues/258)) ([9187acb](https://github.com/derekwinters/connor-multiplying-frogs/commit/9187acb2ae8e26e58ab6903548d324ee4133b531))
* **core:** add the Game type — roster, turn order, and turn phases ([#263](https://github.com/derekwinters/connor-multiplying-frogs/issues/263)) ([9795154](https://github.com/derekwinters/connor-multiplying-frogs/commit/97951549718c6da245aa80bb6517b5dce5f85121))
* **core:** add the Lane type — nine positions, forward, back, home ([#259](https://github.com/derekwinters/connor-multiplying-frogs/issues/259)) ([1f04b67](https://github.com/derekwinters/connor-multiplying-frogs/commit/1f04b67f58b92606ae20309b6c0de51d33d3574b))
* **core:** add the roll and its fixed mapping to the three piles ([#260](https://github.com/derekwinters/connor-multiplying-frogs/issues/260)) ([4cab4c9](https://github.com/derekwinters/connor-multiplying-frogs/commit/4cab4c942a942f88f44e243debf5350121ca6bc2))
* **core:** generate a card, shaped to the pile it was drawn from ([#261](https://github.com/derekwinters/connor-multiplying-frogs/issues/261)) ([ac464f2](https://github.com/derekwinters/connor-multiplying-frogs/commit/ac464f22039ac0bbedb64205a287818194aa1194)), closes [#203](https://github.com/derekwinters/connor-multiplying-frogs/issues/203)
* **core:** grade the answer and resolve the frog's move ([#264](https://github.com/derekwinters/connor-multiplying-frogs/issues/264)) ([3feaad6](https://github.com/derekwinters/connor-multiplying-frogs/commit/3feaad6ceb59fa1bc26150a714b9607bb1a0ecaa))
* **core:** report the working-out grid's shape for a card and row count ([#262](https://github.com/derekwinters/connor-multiplying-frogs/issues/262)) ([edd9623](https://github.com/derekwinters/connor-multiplying-frogs/commit/edd9623664946b123d6735c1841965775115f891))
* **core:** the two ways a game ends, and the standings it produces ([#265](https://github.com/derekwinters/connor-multiplying-frogs/issues/265)) ([3011b65](https://github.com/derekwinters/connor-multiplying-frogs/commit/3011b65ba7bf9a066c70d6425482c9caa9a501ff))
* **router:** screen router — one screen, one dialog, and hardware back ([#267](https://github.com/derekwinters/connor-multiplying-frogs/issues/267)) ([f877858](https://github.com/derekwinters/connor-multiplying-frogs/commit/f877858c5023448c44d6e580b169adfa5e71663e))
* **ui:** build the end-game confirm dialog ([#276](https://github.com/derekwinters/connor-multiplying-frogs/issues/276)) ([226ffb1](https://github.com/derekwinters/connor-multiplying-frogs/commit/226ffb1d09597b21c3369adce5f9c2d5dc502ac3))
* **ui:** build the game board — lanes, frogs, turn banner, and `Roll` ([#273](https://github.com/derekwinters/connor-multiplying-frogs/issues/273)) ([beb569a](https://github.com/derekwinters/connor-multiplying-frogs/commit/beb569a5ec950734dac24899d3c65c60d7dae80b))
* **ui:** build the game setup screen — choose frogs and turn order ([#270](https://github.com/derekwinters/connor-multiplying-frogs/issues/270)) ([9897c8f](https://github.com/derekwinters/connor-multiplying-frogs/commit/9897c8f992412e84c529a652789514669ee37d96))
* **ui:** build the settings dialog ([#274](https://github.com/derekwinters/connor-multiplying-frogs/issues/274)) ([4be6faa](https://github.com/derekwinters/connor-multiplying-frogs/commit/4be6faacc0852e8e29c62d58d4999298a4c01c0c))
* **ui:** build the shared Dialog and the Player chip ([#271](https://github.com/derekwinters/connor-multiplying-frogs/issues/271)) ([bc182f4](https://github.com/derekwinters/connor-multiplying-frogs/commit/bc182f4fc4491c9c3875731ab0c80a0bfc06dadf)), closes [#219](https://github.com/derekwinters/connor-multiplying-frogs/issues/219)
* **ui:** build the title screen to its RESUME/NEW wireframe ([#269](https://github.com/derekwinters/connor-multiplying-frogs/issues/269)) ([77ce62f](https://github.com/derekwinters/connor-multiplying-frogs/commit/77ce62f72b1bc3ec8d12bfc33486431061673b01))
* **ui:** choose uGUI, then build the shared Button and the frog colours ([#268](https://github.com/derekwinters/connor-multiplying-frogs/issues/268)) ([35a2b38](https://github.com/derekwinters/connor-multiplying-frogs/commit/35a2b382355639451638b7f3c396b51cf2d70e3c))
* **ui:** show the die, the pile, and the card you drew ([#275](https://github.com/derekwinters/connor-multiplying-frogs/issues/275)) ([8dad28d](https://github.com/derekwinters/connor-multiplying-frogs/commit/8dad28d3019a2ee26632a82da5c312bda1963a63))
* **ui:** show who won and where everybody got to when a game ends ([#272](https://github.com/derekwinters/connor-multiplying-frogs/issues/272)) ([a908509](https://github.com/derekwinters/connor-multiplying-frogs/commit/a908509d55fa530e1c489c49e0dc4588afe36852))
* **ui:** the answer result dialog and the frog's hop ([#278](https://github.com/derekwinters/connor-multiplying-frogs/issues/278)) ([6e903ab](https://github.com/derekwinters/connor-multiplying-frogs/commit/6e903ab1c3a6e7cd23849108ff09c0c5899c4582))
* **working-out-grid:** draw the grid and keypad from Core's grid model ([#277](https://github.com/derekwinters/connor-multiplying-frogs/issues/277)) ([f240273](https://github.com/derekwinters/connor-multiplying-frogs/commit/f2402732666ec342baaeb5655bd03a64e2c134af))


### Bug Fixes

* **build:** pass the build's inputs on Unity's command line ([#253](https://github.com/derekwinters/connor-multiplying-frogs/issues/253)) ([50fa53b](https://github.com/derekwinters/connor-multiplying-frogs/commit/50fa53bd446f4d77faa8da52f1b35d7d4d27c998))
* **ci:** make the docs reconciliation gate match CLAUDE.md rule 9 ([#251](https://github.com/derekwinters/connor-multiplying-frogs/issues/251)) ([5e26c5f](https://github.com/derekwinters/connor-multiplying-frogs/commit/5e26c5f463d090a8a537cf6a135caa4b54567d29)), closes [#176](https://github.com/derekwinters/connor-multiplying-frogs/issues/176)
* **game:** record a frog's finish when it reaches the End log ([#281](https://github.com/derekwinters/connor-multiplying-frogs/issues/281)) ([53df092](https://github.com/derekwinters/connor-multiplying-frogs/commit/53df092b0cb2d0b47546ed5a6b63d11777fde519))
* **gatekeeper:** log the reactive-triage fire instead of discarding it ([#232](https://github.com/derekwinters/connor-multiplying-frogs/issues/232)) ([00c4371](https://github.com/derekwinters/connor-multiplying-frogs/commit/00c437109e017c9cb28c8b93a6fbf37e904bcf5a)), closes [#231](https://github.com/derekwinters/connor-multiplying-frogs/issues/231)
* **gatekeeper:** recognise claude_code_session_url as a real fire ([#241](https://github.com/derekwinters/connor-multiplying-frogs/issues/241)) ([cb94905](https://github.com/derekwinters/connor-multiplying-frogs/commit/cb949053ff0df27a6a56a5cb5f20dcf1f9454148)), closes [#240](https://github.com/derekwinters/connor-multiplying-frogs/issues/240)
* **gatekeeper:** return the triage endpoint's error body instead of raising ([#237](https://github.com/derekwinters/connor-multiplying-frogs/issues/237)) ([f6d7ad4](https://github.com/derekwinters/connor-multiplying-frogs/commit/f6d7ad40c1dd9f9bb8b3890248baa52ba651d294)), closes [#236](https://github.com/derekwinters/connor-multiplying-frogs/issues/236)
* **gatekeeper:** send the anthropic-version header the triage endpoint requires ([#239](https://github.com/derekwinters/connor-multiplying-frogs/issues/239)) ([ba66aa1](https://github.com/derekwinters/connor-multiplying-frogs/commit/ba66aa17275e512ff4781c0734be476770ba35d8)), closes [#238](https://github.com/derekwinters/connor-multiplying-frogs/issues/238)

## [0.1.1](https://github.com/derekwinters/connor-multiplying-frogs/compare/v0.1.0...v0.1.1) (2026-08-10)


### Bug Fixes

* **ci:** drop package-name so release-please can tag releases ([#206](https://github.com/derekwinters/connor-multiplying-frogs/issues/206)) ([9d70ab9](https://github.com/derekwinters/connor-multiplying-frogs/commit/9d70ab921297d10e61ed79670525f8be0cc8b925)), closes [#205](https://github.com/derekwinters/connor-multiplying-frogs/issues/205)
* **ci:** read the release APKs from the directory Unity writes them to ([#215](https://github.com/derekwinters/connor-multiplying-frogs/issues/215)) ([450a5cc](https://github.com/derekwinters/connor-multiplying-frogs/commit/450a5cc6427eb9253eb89ee3ddfd5859fc42e089)), closes [#212](https://github.com/derekwinters/connor-multiplying-frogs/issues/212)

## [0.1.0](https://github.com/derekwinters/connor-multiplying-frogs/compare/v0.0.1...v0.1.0) (2026-08-09)


### Features

* add /VERSION with the release-please marker ([#108](https://github.com/derekwinters/connor-multiplying-frogs/issues/108)) ([af49c39](https://github.com/derekwinters/connor-multiplying-frogs/commit/af49c399d23fcd1aab8905e3850d343ae5f0a923)), closes [#29](https://github.com/derekwinters/connor-multiplying-frogs/issues/29)
* **agents:** add the development agent ([#128](https://github.com/derekwinters/connor-multiplying-frogs/issues/128)) ([46d3799](https://github.com/derekwinters/connor-multiplying-frogs/commit/46d37991e89b53b34cd5df0b4cdc4e50c71b6748)), closes [#45](https://github.com/derekwinters/connor-multiplying-frogs/issues/45)
* **build:** add the Hello World scene and prove it produces a debug APK ([#179](https://github.com/derekwinters/connor-multiplying-frogs/issues/179)) ([44f9673](https://github.com/derekwinters/connor-multiplying-frogs/commit/44f9673c423116b80335f309d79b986136d9686f)), closes [#28](https://github.com/derekwinters/connor-multiplying-frogs/issues/28)
* **build:** stamp the version into PlayerSettings at build time ([#116](https://github.com/derekwinters/connor-multiplying-frogs/issues/116)) ([216a861](https://github.com/derekwinters/connor-multiplying-frogs/commit/216a8611a7754c79b4f286d90cc5320c0cc79c93)), closes [#33](https://github.com/derekwinters/connor-multiplying-frogs/issues/33)
* **gatekeeper:** replay comment commands the comment workflow missed ([#197](https://github.com/derekwinters/connor-multiplying-frogs/issues/197)) ([d04309f](https://github.com/derekwinters/connor-multiplying-frogs/commit/d04309fdc4a9b76e3d913775e7148927e955453e)), closes [#161](https://github.com/derekwinters/connor-multiplying-frogs/issues/161)
* **labels:** add the area/type/pipeline label taxonomy as code ([#87](https://github.com/derekwinters/connor-multiplying-frogs/issues/87)) ([0b61091](https://github.com/derekwinters/connor-multiplying-frogs/commit/0b610916c9fbbd1391c3655f8d9b94898a0dcc25)), closes [#1](https://github.com/derekwinters/connor-multiplying-frogs/issues/1)
* **pipeline:** add blocker auto-revisit to the gatekeeper ([#139](https://github.com/derekwinters/connor-multiplying-frogs/issues/139)) ([55ef61c](https://github.com/derekwinters/connor-multiplying-frogs/commit/55ef61c6bfee5a591a4a36d95b0f4b8d674a3a72)), closes [#56](https://github.com/derekwinters/connor-multiplying-frogs/issues/56)
* **pipeline:** add build-queue selection to pipeline-dev ([#148](https://github.com/derekwinters/connor-multiplying-frogs/issues/148)) ([1416919](https://github.com/derekwinters/connor-multiplying-frogs/commit/1416919047586cf128081c4e81d72eef175949eb)), closes [#66](https://github.com/derekwinters/connor-multiplying-frogs/issues/66)
* **pipeline:** add drift detection to pipeline-reconcile ([#150](https://github.com/derekwinters/connor-multiplying-frogs/issues/150)) ([cb1f072](https://github.com/derekwinters/connor-multiplying-frogs/commit/cb1f0725e7ea37e5e3249d1403f9e675f4330262)), closes [#68](https://github.com/derekwinters/connor-multiplying-frogs/issues/68)
* **pipeline:** add re-fire repair detection to triage-issue ([#146](https://github.com/derekwinters/connor-multiplying-frogs/issues/146)) ([97fc3fb](https://github.com/derekwinters/connor-multiplying-frogs/commit/97fc3fbd66bc1695d1e520827ae3a88e016e9c6e)), closes [#65](https://github.com/derekwinters/connor-multiplying-frogs/issues/65)
* **pipeline:** add the /approve milestone-presence gate ([#137](https://github.com/derekwinters/connor-multiplying-frogs/issues/137)) ([433081a](https://github.com/derekwinters/connor-multiplying-frogs/commit/433081a24a98b5d21215da4c1f2ffd24147b2f77)), closes [#54](https://github.com/derekwinters/connor-multiplying-frogs/issues/54)
* **pipeline:** add the dashboard renderer core ([#152](https://github.com/derekwinters/connor-multiplying-frogs/issues/152)) ([4c32fa0](https://github.com/derekwinters/connor-multiplying-frogs/commit/4c32fa0c3870ff936ea4682015205db529c145e7)), closes [#70](https://github.com/derekwinters/connor-multiplying-frogs/issues/70)
* **pipeline:** add the gatekeeper I/O glue ([#155](https://github.com/derekwinters/connor-multiplying-frogs/issues/155)) ([a85a6fb](https://github.com/derekwinters/connor-multiplying-frogs/commit/a85a6fb0e5f0d6ee6d12f763a6cc8983673135c6)), closes [#59](https://github.com/derekwinters/connor-multiplying-frogs/issues/59)
* **pipeline:** add the gatekeeper's deterministic command parser ([#136](https://github.com/derekwinters/connor-multiplying-frogs/issues/136)) ([6c4946f](https://github.com/derekwinters/connor-multiplying-frogs/commit/6c4946f801b663418fccccfee32b9f9406375626)), closes [#53](https://github.com/derekwinters/connor-multiplying-frogs/issues/53)
* **pipeline:** add the gatekeeper's per-issue snapshot builder ([#140](https://github.com/derekwinters/connor-multiplying-frogs/issues/140)) ([538aa0f](https://github.com/derekwinters/connor-multiplying-frogs/commit/538aa0f882651db007d8f1bee09fa251f51010ea)), closes [#57](https://github.com/derekwinters/connor-multiplying-frogs/issues/57)
* **pipeline:** add the milestone-order gate ([#138](https://github.com/derekwinters/connor-multiplying-frogs/issues/138)) ([a496f27](https://github.com/derekwinters/connor-multiplying-frogs/commit/a496f27e285a6b2e875074afe4b4fb3b8a893bb2)), closes [#55](https://github.com/derekwinters/connor-multiplying-frogs/issues/55)
* **pipeline:** add the remaining dashboard sections and unblocker stars ([#153](https://github.com/derekwinters/connor-multiplying-frogs/issues/153)) ([1acec3c](https://github.com/derekwinters/connor-multiplying-frogs/commit/1acec3cd4c48223645d1bb03921fdbd210377664)), closes [#71](https://github.com/derekwinters/connor-multiplying-frogs/issues/71)
* **pipeline:** add triage discovery to pipeline-analysis ([#143](https://github.com/derekwinters/connor-multiplying-frogs/issues/143)) ([c408027](https://github.com/derekwinters/connor-multiplying-frogs/commit/c40802780233803f3e9724ed0ec8f3a8f0f225af)), closes [#62](https://github.com/derekwinters/connor-multiplying-frogs/issues/62)
* **pipeline:** compute the gatekeeper's label merge, acks, and watermark ([#141](https://github.com/derekwinters/connor-multiplying-frogs/issues/141)) ([b4710d2](https://github.com/derekwinters/connor-multiplying-frogs/commit/b4710d221e696b31c1db32458044ada7026e4769)), closes [#58](https://github.com/derekwinters/connor-multiplying-frogs/issues/58)
* **pipeline:** fire reactive triage the moment an issue enters ai-triage ([#142](https://github.com/derekwinters/connor-multiplying-frogs/issues/142)) ([f8e6d9e](https://github.com/derekwinters/connor-multiplying-frogs/commit/f8e6d9e1b3e6e9c8e746ad740de6b0104991325d)), closes [#60](https://github.com/derekwinters/connor-multiplying-frogs/issues/60)
* **skills:** bring over the ci-watch skill ([#131](https://github.com/derekwinters/connor-multiplying-frogs/issues/131)) ([01352a7](https://github.com/derekwinters/connor-multiplying-frogs/commit/01352a72d879b0ff2138376d974de4bfd03cfd62)), closes [#48](https://github.com/derekwinters/connor-multiplying-frogs/issues/48)
* **skills:** bring over the core-unity-split reference skill ([#133](https://github.com/derekwinters/connor-multiplying-frogs/issues/133)) ([0986a87](https://github.com/derekwinters/connor-multiplying-frogs/commit/0986a8799fb935545745f407a3634c1e752aa519)), closes [#50](https://github.com/derekwinters/connor-multiplying-frogs/issues/50)
* **skills:** bring over the issue-blockers skill ([#134](https://github.com/derekwinters/connor-multiplying-frogs/issues/134)) ([08a018b](https://github.com/derekwinters/connor-multiplying-frogs/commit/08a018b9c3bdb03147b64f89d7cdab302fbe4368)), closes [#51](https://github.com/derekwinters/connor-multiplying-frogs/issues/51)
* **skills:** bring over the milestone-ops skill ([#135](https://github.com/derekwinters/connor-multiplying-frogs/issues/135)) ([a442990](https://github.com/derekwinters/connor-multiplying-frogs/commit/a4429908953abc206f598fe5ab7686f27ba7242a)), closes [#52](https://github.com/derekwinters/connor-multiplying-frogs/issues/52)
* **skills:** bring over the release-flow skill ([#117](https://github.com/derekwinters/connor-multiplying-frogs/issues/117)) ([15291f9](https://github.com/derekwinters/connor-multiplying-frogs/commit/15291f910b84207b300cd2b46f96f60253d0760e)), closes [#34](https://github.com/derekwinters/connor-multiplying-frogs/issues/34)
* **skills:** bring over the run-tests skill ([#130](https://github.com/derekwinters/connor-multiplying-frogs/issues/130)) ([cf3e224](https://github.com/derekwinters/connor-multiplying-frogs/commit/cf3e224d8b73c9768f7a7b7a4e9c37728dc01420)), closes [#47](https://github.com/derekwinters/connor-multiplying-frogs/issues/47)
* **skills:** bring over the scaffold-core skill ([#132](https://github.com/derekwinters/connor-multiplying-frogs/issues/132)) ([78a063c](https://github.com/derekwinters/connor-multiplying-frogs/commit/78a063cdce2eae7d0a6b95ea1b4f8c8bff16ad4a)), closes [#49](https://github.com/derekwinters/connor-multiplying-frogs/issues/49)


### Bug Fixes

* **build:** apply the project's own settings, and build for tablets in landscape ([#184](https://github.com/derekwinters/connor-multiplying-frogs/issues/184)) ([7113309](https://github.com/derekwinters/connor-multiplying-frogs/commit/7113309ac28fa7c4383ff7ed30e9e92a3a8c50fb))
* **ci:** put the version in the release PR title ([#115](https://github.com/derekwinters/connor-multiplying-frogs/issues/115)) ([3c7455d](https://github.com/derekwinters/connor-multiplying-frogs/commit/3c7455df54c3006f4d02b46d695a50597db43eae))
* **pipeline:** paginate every list read, and seed the dashboard issue ([#164](https://github.com/derekwinters/connor-multiplying-frogs/issues/164)) ([c1113a5](https://github.com/derekwinters/connor-multiplying-frogs/commit/c1113a53b95849995759b7174d44338eafa6da51)), closes [#78](https://github.com/derekwinters/connor-multiplying-frogs/issues/78)
* **triage:** wire up reactive triage, and make the hand-back a write that cannot be skipped ([#194](https://github.com/derekwinters/connor-multiplying-frogs/issues/194)) ([f87184e](https://github.com/derekwinters/connor-multiplying-frogs/commit/f87184ed1836cf86ba832637910e7ec6297086b3))
