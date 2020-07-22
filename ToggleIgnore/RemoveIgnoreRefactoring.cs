namespace ToggleIgnore
{
   using System.Composition;
   using System.Linq;
   using System.Threading.Tasks;
   using Microsoft.CodeAnalysis;
   using Microsoft.CodeAnalysis.CodeActions;
   using Microsoft.CodeAnalysis.CodeRefactorings;
   using Microsoft.CodeAnalysis.CSharp.Syntax;
   using Microsoft.CodeAnalysis.FindSymbols;

   [ ExportCodeRefactoringProvider( LanguageNames.CSharp, Name = nameof(RemoveIgnoreRefactoring) ), Shared ]
   public class RemoveIgnoreRefactoring: CodeRefactoringProvider
   {
      public sealed override async Task ComputeRefactoringsAsync( CodeRefactoringContext context )
      {
         SyntaxNode root = await context.Document.GetSyntaxRootAsync( context.CancellationToken ).ConfigureAwait( false );
         SemanticModel semanticModel = await context.Document.GetSemanticModelAsync( context.CancellationToken ).ConfigureAwait( false );

         INamedTypeSymbol ignoreType = semanticModel.Compilation.GetTypeByMetadataName( "NUnit.Framework.IgnoreAttribute" );
         INamedTypeSymbol testType = semanticModel.Compilation.GetTypeByMetadataName( "NUnit.Framework.TestAttribute" );

         if( testType == null || ignoreType == null ) return;

         ISymbol methodSymbol = await SymbolFinder.FindSymbolAtPositionAsync( context.Document, context.Span.Start, context.CancellationToken ) as IMethodSymbol ??
                                semanticModel.GetEnclosingSymbol( context.Span.Start, context.CancellationToken ) as IMethodSymbol;
         if( methodSymbol == null ) return;

         bool hasTestAttribute = methodSymbol.GetAttributes().Any( a => a.AttributeClass.Equals( testType ) );
         AttributeData ignoreAttribute = methodSymbol.GetAttributes().FirstOrDefault( a => a.AttributeClass.Equals( ignoreType ) );
         if( !hasTestAttribute || ignoreAttribute == null ) return;

         var ignoreAttributeSyntax = root.FindNode( ignoreAttribute.ApplicationSyntaxReference.Span ) as AttributeSyntax;
         MethodDeclarationSyntax methodDeclaration = root.FindToken( context.Span.Start ).Parent.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().First();

         context.RegisterRefactoring( CodeAction.Create( "Remove NUnit Ignore attributes",
                                                         token => RemoveIgnore( context.Document,
                                                                                root,
                                                                                methodDeclaration,
                                                                                ignoreAttributeSyntax ) ) );
      }

      private static Task<Document> RemoveIgnore( Document document,
                                                  SyntaxNode root,
                                                  MethodDeclarationSyntax methodDeclaration,
                                                  AttributeSyntax ignoreAttribute )
      {
         MethodDeclarationSyntax newMethodDeclaration = methodDeclaration.RemoveNode( ignoreAttribute, SyntaxRemoveOptions.KeepNoTrivia );

         SyntaxNode newRoot = root.ReplaceNode( methodDeclaration, newMethodDeclaration );

         return Task.FromResult( document.WithSyntaxRoot( newRoot ) );
      }
   }
}
