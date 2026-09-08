// swift-tools-version:5.9
import PackageDescription

let package = Package(
    name: "Unfold",
    platforms: [
        .macOS(.v13)
    ],
    products: [
        // Consumed by the Xcode app target (Unfold.xcodeproj) for the
        // App Store production build. The module name importers use is
        // still the target name "Unfold" — this product name only needs
        // to differ from the CLI executable product below.
        .library(name: "UnfoldKit", targets: ["Unfold"]),
        // Local dev/debug pipeline only (`swift build` / `swift run` /
        // Scripts/make-app-bundle.sh). Kept named "Unfold" so the existing
        // shell script's binary path doesn't need to change.
        .executable(name: "Unfold", targets: ["UnfoldCLI"])
    ],
    targets: [
        .target(
            name: "Unfold",
            path: "Sources/Unfold",
            resources: [
                .copy("Resources/Characters")
            ]
        ),
        .executableTarget(
            name: "UnfoldCLI",
            dependencies: ["Unfold"],
            path: "Sources/UnfoldCLI"
        ),
        .testTarget(
            name: "UnfoldTests",
            dependencies: ["Unfold"],
            path: "Tests/UnfoldTests"
        )
    ]
)
