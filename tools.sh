#!/usr/bin/env bash

# MU Online MonoGame Build & Run Tools
# Usage: ./tools.sh [command] [options]

set -e  # Exit on any error

# Check if we're running in bash
if [ -z "$BASH_VERSION" ]; then
    echo "This script requires bash. Please run with: bash $0 $*"
    exit 1
fi

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SOLUTION_FILE="$PROJECT_ROOT/MuOnline.sln"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Platform configurations
PLATFORM_NAMES=("win" "mac" "linux" "android" "ios")
PROJECT_NAMES=("MuWin" "MuMac" "MuLinux" "MuAndroid" "MuIos")
RUNTIME_IDS=("win-x64" "osx-x64" "linux-x64" "android" "ios")

# Helper functions
print_header() {
    echo -e "${BLUE}================================${NC}"
    echo -e "${BLUE}$1${NC}"
    echo -e "${BLUE}================================${NC}"
}

print_success() {
    echo -e "${GREEN}✓ $1${NC}"
}

print_warning() {
    echo -e "${YELLOW}⚠ $1${NC}"
}

print_error() {
    echo -e "${RED}✗ $1${NC}"
}

check_requirements() {
    print_header "Checking Requirements"
    
    # Check .NET SDK
    if ! command -v dotnet &> /dev/null; then
        print_error ".NET SDK is not installed or not in PATH"
        exit 1
    fi
    
    local dotnet_version
    dotnet_version=$(dotnet --version)
    print_success ".NET SDK $dotnet_version found"
    
    # Check if solution file exists
    if [ ! -f "$SOLUTION_FILE" ]; then
        print_error "Solution file not found: $SOLUTION_FILE"
        exit 1
    fi
    
    print_success "Solution file found"
}

show_help() {
    echo "MU Online MonoGame Build & Run Tools"
    echo ""
    echo "Usage: $0 [command] [platform] [configuration]"
    echo ""
    echo "Commands:"
    echo "  build      Build project for specified platform"
    echo "  run        Build and run project for specified platform"
    echo "  clean      Clean build artifacts"
    echo "  restore    Restore NuGet packages"
    echo "  publish    Publish self-contained executable"
    echo "  test       Run tests (if any)"
    echo "  list       List available platforms"
    echo "  setup      Initial project setup"
    echo "  help       Show this help message"
    echo ""
    echo "Platforms:"
    echo "  win        Windows (MuWin)"
    echo "  mac        macOS (MuMac)"
    echo "  linux      Linux (MuLinux)"
    echo "  android    Android (MuAndroid)"
    echo "  ios        iOS (MuIos)"
    echo "  all        All platforms"
    echo ""
    echo "Configurations:"
    echo "  Debug      Debug build (default)"
    echo "  Release    Release build"
    echo ""
    echo "Examples:"
    echo "  $0 build mac                    # Build macOS version (Debug)"
    echo "  $0 build win Release            # Build Windows version (Release)"
    echo "  $0 run mac                      # Build and run macOS version"
    echo "  $0 publish linux Release       # Publish Linux version"
    echo "  $0 clean all                    # Clean all platforms"
}

get_platform_index() {
    local platform=$1
    for i in "${!PLATFORM_NAMES[@]}"; do
        if [ "${PLATFORM_NAMES[$i]}" = "$platform" ]; then
            echo "$i"
            return
        fi
    done
    echo "-1"
}

validate_platform() {
    local platform=$1
    if [ "$platform" != "all" ]; then
        local index=$(get_platform_index "$platform")
        if [ "$index" = "-1" ]; then
            print_error "Invalid platform: $platform"
            echo "Available platforms: ${PLATFORM_NAMES[*]} all"
            exit 1
        fi
    fi
}

get_project_name() {
    local platform=$1
    local index=$(get_platform_index "$platform")
    echo "${PROJECT_NAMES[$index]}"
}

get_runtime_id() {
    local platform=$1
    local index=$(get_platform_index "$platform")
    echo "${RUNTIME_IDS[$index]}"
}

get_project_path() {
    local platform=$1
    local project_name=$(get_project_name "$platform")
    echo "$PROJECT_ROOT/$project_name"
}

get_output_path() {
    local platform=$1
    local config=$2
    local project_name=$(get_project_name "$platform")
    local runtime_id=$(get_runtime_id "$platform")
    echo "$PROJECT_ROOT/$project_name/bin/$config/net9.0/$runtime_id"
}

get_executable_name() {
    local platform=$1
    local project_name=$(get_project_name "$platform")
    case $platform in
        win)
            echo "$project_name.exe"
            ;;
        *)
            echo "$project_name"
            ;;
    esac
}

restore_packages() {
    local platform=$1
    print_header "Restoring NuGet Packages"
    cd "$PROJECT_ROOT"
    
    if [ -n "$platform" ] && [ "$platform" != "all" ]; then
        # Restore specific project
        local project_name
        project_name=$(get_project_name "$platform")
        dotnet restore "$project_name"
    else
        # Try to restore solution, but continue on error for cross-platform issues
        dotnet restore "$SOLUTION_FILE" || {
            print_warning "Solution restore failed, trying individual projects..."
            for p in "${PLATFORM_NAMES[@]}"; do
                local proj_name
                proj_name=$(get_project_name "$p")
                if [ -d "$PROJECT_ROOT/$proj_name" ]; then
                    echo "Restoring $proj_name..."
                    dotnet restore "$proj_name" || echo "Failed to restore $proj_name"
                fi
            done
        }
    fi
    print_success "Package restore completed"
}

clean_project() {
    local platform=$1
    local project_name
    
    print_header "Cleaning $platform"
    cd "$PROJECT_ROOT"
    
    if [ "$platform" == "all" ]; then
        dotnet clean "$SOLUTION_FILE"
        print_success "All projects cleaned"
    else
        project_name=$(get_project_name "$platform")
        dotnet clean "$project_name"
        print_success "$project_name cleaned"
    fi
}

build_project() {
    local platform=$1
    local config=${2:-Debug}
    local project_name
    project_name=$(get_project_name "$platform")
    
    print_header "Building $project_name ($config)"
    cd "$PROJECT_ROOT"
    
    # Build the project
    dotnet build "$project_name" -c "$config" --no-restore
    
    print_success "$project_name built successfully"
}

run_project() {
    local platform=$1
    local config=${2:-Debug}
    local project_name
    project_name=$(get_project_name "$platform")
    
    # First build the project
    build_project "$platform" "$config"
    
    print_header "Running $project_name"
    
    case $platform in
        mac|linux|win)
            local output_path
            local executable
            local full_path
            output_path=$(get_output_path "$platform" "$config")
            executable=$(get_executable_name "$platform")
            full_path="$output_path/$executable"
            
            if [ -f "$full_path" ]; then
                print_success "Starting $project_name..."
                cd "$PROJECT_ROOT"
                if [ "$platform" == "win" ]; then
                    # For Windows, might need wine on non-Windows systems
                    "$full_path"
                else
                    "$full_path"
                fi
            else
                print_error "Executable not found: $full_path"
                exit 1
            fi
            ;;
        android)
            print_warning "Android projects need to be deployed to a device or emulator"
            print_warning "Use: dotnet build $project_name -f net9.0-android"
            ;;
        ios)
            print_warning "iOS projects need to be built with Xcode or deployed to a device"
            print_warning "Use: dotnet build $project_name -f net9.0-ios"
            ;;
    esac
}

publish_project() {
    local platform=$1
    local config=${2:-Release}
    local project_name
    local runtime_id
    project_name=$(get_project_name "$platform")
    runtime_id=$(get_runtime_id "$platform")
    
    print_header "Publishing $project_name ($config)"
    cd "$PROJECT_ROOT"
    
    case $platform in
        mac|linux|win)
            dotnet publish "$project_name" \
                -c "$config" \
                -r "$runtime_id" \
                --self-contained true \
                --no-restore \
                -p:PublishSingleFile=true \
                -p:PublishTrimmed=false
            
            local publish_path="$PROJECT_ROOT/$project_name/bin/$config/net9.0/$runtime_id/publish"
            print_success "$project_name published to: $publish_path"
            ;;
        android|ios)
            print_warning "$platform publishing requires platform-specific tools"
            dotnet build "$project_name" -c "$config" --no-restore
            ;;
    esac
}

setup_project() {
    print_header "Setting up MU Online Project"
    
    check_requirements
    restore_packages ""
    
    print_header "Checking Data Directory"
    if [ ! -d "$PROJECT_ROOT/Data" ]; then
        print_warning "Data directory not found at $PROJECT_ROOT/Data"
        print_warning "Make sure to place your MU Online data files in the Data directory"
        print_warning "Or update the DataPath in Client.Main/Constants.cs"
    else
        print_success "Data directory found"
    fi
    
    print_success "Project setup complete!"
    echo ""
    echo "Next steps:"
    echo "1. Ensure your MU Online data files are in the correct location"
    echo "2. Build a platform: $0 build mac"
    echo "3. Run the game: $0 run mac"
}

list_platforms() {
    print_header "Available Platforms"
    for i in "${!PLATFORM_NAMES[@]}"; do
        echo "  ${PLATFORM_NAMES[$i]} -> ${PROJECT_NAMES[$i]}"
    done
    echo "  all -> All platforms"
}

run_tests() {
    print_header "Running Tests"
    cd "$PROJECT_ROOT"
    
    if dotnet test --list-tests 2>/dev/null | grep -q "No test"; then
        print_warning "No tests found in solution"
    else
        dotnet test
        print_success "Tests completed"
    fi
}

# Main script logic
main() {
    local command=${1:-help}
    local platform=$2
    local config=${3:-Debug}
    
    case $command in
        help|--help|-h)
            show_help
            ;;
        setup)
            setup_project
            ;;
        list)
            list_platforms
            ;;
        restore)
            check_requirements
            restore_packages ""
            ;;
        clean)
            check_requirements
            if [ -z "$platform" ]; then
                platform="all"
            fi
            validate_platform "$platform"
            if [ "$platform" == "all" ]; then
                clean_project "all"
            else
                clean_project "$platform"
            fi
            ;;
        build)
            if [ -z "$platform" ]; then
                print_error "Platform required for build command"
                echo "Usage: $0 build [platform] [configuration]"
                exit 1
            fi
            check_requirements
            validate_platform "$platform"
            if [ "$platform" == "all" ]; then
                for p in "${PLATFORM_NAMES[@]}"; do
                    restore_packages "$p"
                    build_project "$p" "$config"
                done
            else
                restore_packages "$platform"
                build_project "$platform" "$config"
            fi
            ;;
        run)
            if [ -z "$platform" ]; then
                print_error "Platform required for run command"
                echo "Usage: $0 run [platform] [configuration]"
                exit 1
            fi
            check_requirements
            validate_platform "$platform"
            if [ "$platform" == "all" ]; then
                print_error "Cannot run all platforms simultaneously"
                exit 1
            fi
            restore_packages "$platform"
            run_project "$platform" "$config"
            ;;
        publish)
            if [ -z "$platform" ]; then
                print_error "Platform required for publish command"
                echo "Usage: $0 publish [platform] [configuration]"
                exit 1
            fi
            check_requirements
            validate_platform "$platform"
            if [ "$platform" == "all" ]; then
                for p in "${PLATFORM_NAMES[@]}"; do
                    restore_packages "$p"
                    publish_project "$p" "$config"
                done
            else
                restore_packages "$platform"
                publish_project "$platform" "$config"
            fi
            ;;
        test)
            check_requirements
            run_tests
            ;;
        *)
            print_error "Unknown command: $command"
            echo ""
            show_help
            exit 1
            ;;
    esac
}

# Run main function with all arguments
main "$@"
