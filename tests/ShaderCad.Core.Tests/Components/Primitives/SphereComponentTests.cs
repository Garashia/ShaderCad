using System.Numerics;
using FluentAssertions;
using ShaderCad.Core.Components.Primitives;
using ShaderCad.Core.Diagnostics;
using ShaderCad.Core.Geometry;
using ShaderCad.Core.Models;
using Xunit;

namespace ShaderCad.Core.Tests.Components.Primitives;

public class SphereComponentTests
{
    [Fact]
    public void Validate_PositiveRadius_ReturnsSuccess()
    {
        // Arrange
        var node = new CadNode();
        var sphere = node.AddComponent<SphereComponent>();
        sphere.Radius = 5.0;

        // Act
        var result = sphere.Validate();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Validate_NegativeOrZeroRadius_ReturnsError()
    {
        // Arrange
        var node = new CadNode();
        var sphere = node.AddComponent<SphereComponent>();
        sphere.Radius = -1.0;

        // Act
        var result = sphere.Validate();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Diagnostics.Should().ContainSingle()
            .Which.Level.Should().Be(CadErrorLevel.Error);
    }

    [Fact]
    public void BuildGeometry_CallsAddSphereWithCorrectRadius()
    {
        // Arrange
        var sphere = new SphereComponent { Radius = 3.5 };
        var mockBuilder = new MockMeshBuilder();

        // Act
        sphere.BuildGeometry(mockBuilder);

        // Assert
        mockBuilder.AddedSphereRadius.Should().Be(3.5);
        mockBuilder.AddedSphereCenter.Should().Be(Vector3.Zero);
    }

    // 簡単なテスト用のモックビルダー
    private class MockMeshBuilder : IMeshBuilder
    {
        public double? AddedSphereRadius { get; private set; }
        public Vector3? AddedSphereCenter { get; private set; }

        public void AddSphere(double radius, Vector3 center)
        {
            AddedSphereRadius = radius;
            AddedSphereCenter = center;
        }

        public void AddCube(Vector3 size, Vector3 center)
        {
            // 未使用
        }
    }
}
