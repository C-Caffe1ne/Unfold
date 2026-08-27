// swift-tools-version:5.9
import PackageDescription

let package = Package(
    name: "Unfold",
    platforms: [
        .macOS(.v13)
    ],
    targets: [
        .executableTarget(
            name: "Unfold",
            path: "Sources/Unfold",
            resources: [
                .copy("Resources/Characters")
            ]
        )
    ]
)
