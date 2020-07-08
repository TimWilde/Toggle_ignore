namespace ToggleIgnore
{
   using System;
   using System.Collections.Generic;
   using System.Composition;
   using System.Linq;
   using System.Threading.Tasks;
   using Microsoft.CodeAnalysis;
   using Microsoft.CodeAnalysis.CodeActions;
   using Microsoft.CodeAnalysis.CodeRefactorings;
   using Microsoft.CodeAnalysis.CSharp;
   using Microsoft.CodeAnalysis.CSharp.Syntax;
   using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

   [ ExportCodeRefactoringProvider( LanguageNames.CSharp, Name = nameof(RemoveIgnoreRefactoring) ), Shared ]
   public class RemoveIgnoreRefactoring: CodeRefactoringProvider
   {
      public sealed override async Task ComputeRefactoringsAsync( CodeRefactoringContext context )
      {
         SemanticModel model = await context.Document.GetSemanticModelAsync( context.CancellationToken );
         if( model == null ) return;

         SyntaxNode root = await model.SyntaxTree.GetRootAsync( context.CancellationToken );
         SyntaxNode node = root.FindNode( context.Span );

         var nUnitUsingContainer = node.FirstAncestorOrSelf<SyntaxNode>( n => n.ChildNodes().Any( x => x is UsingDirectiveSyntax usingDirective &&
                                                                                                       usingDirective.Name.ToFullString().StartsWith( "NUnit" ) ) );
         if( nUnitUsingContainer == null ) return;

         var methodDeclaration = node.FirstAncestorOrSelf<MethodDeclarationSyntax>();
         if( methodDeclaration == null ) return;

         var ignoreAttributes = new List<AttributeSyntax>();
         var testAttributes = new List<AttributeSyntax>();
         foreach( AttributeListSyntax attributeList in methodDeclaration.AttributeLists )
         {
            foreach( AttributeSyntax attribute in attributeList.Attributes )
            {
               if( attribute.Name.ToFullString().Equals( "Ignore" ) )
                  ignoreAttributes.Add( attribute );

               if( attribute.Name.ToFullString().StartsWith( "Test" ) )
                  testAttributes.Add( attribute );
            }
         }

         if( testAttributes.Count == 0 ) return;

         if( ignoreAttributes.Count == 0 )
            context.RegisterRefactoring( CodeAction.Create( "Add ignore attribute",
                                                            t => Task.FromResult( AddIgnore( context.Document,
                                                                                             root,
                                                                                             methodDeclaration ) ) ) );
         else
            context.RegisterRefactoring( CodeAction.Create( "Remove ignore attributes",
                                                            t => Task.FromResult( RemoveIgnore( context.Document,
                                                                                                root,
                                                                                                methodDeclaration,
                                                                                                ignoreAttributes ) ) ) );
      }

      private Document AddIgnore( Document document, SyntaxNode root, MethodDeclarationSyntax methodDeclaration )
      {
         AttributeListSyntax attributesList = methodDeclaration.AttributeLists.First();
         AttributeListSyntax newAttributeList = attributesList.AddAttributes( BuildAttribute( "Ignore", $"Ignored since {DateTime.Now.ToShortDateString()}" ) );

         MethodDeclarationSyntax newMethodDeclaration = methodDeclaration.ReplaceNode( attributesList, newAttributeList );
         SyntaxNode newRoot = root.ReplaceNode( methodDeclaration, newMethodDeclaration );

         return document.WithSyntaxRoot( newRoot );

         AttributeSyntax BuildAttribute( string name, string message )
         {
            return Attribute( IdentifierName( name ) )
               .WithArgumentList(
                  AttributeArgumentList(
                     SingletonSeparatedList(
                        AttributeArgument(
                           LiteralExpression(
                              SyntaxKind.StringLiteralExpression,
                              Literal( message ) ) ) ) ) );
         }
      }

      private Document RemoveIgnore( Document document,
                                     SyntaxNode root,
                                     MethodDeclarationSyntax methodDeclaration,
                                     IEnumerable<AttributeSyntax> ignoreAttributes )
      {
         MethodDeclarationSyntax newMethodDeclaration = methodDeclaration;

         foreach( AttributeSyntax attribute in ignoreAttributes )
            newMethodDeclaration = newMethodDeclaration.RemoveNode( attribute, SyntaxRemoveOptions.KeepNoTrivia );

         SyntaxNode newRoot = root.ReplaceNode( methodDeclaration, newMethodDeclaration );

         return document.WithSyntaxRoot( newRoot );
      }
   }
}
