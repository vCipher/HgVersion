﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Channels;
using Mercurial;
using Mercurial.Gui;
using VCSVersion;
using VCSVersion.VCS;

namespace HgVersion.VCS
{
    /// <inheritdoc />
    public sealed class HgRepository : IHgRepository
    {
        private readonly Repository _repository;

        /// <summary>
        /// Creates an instance of <see cref="HgRepository"/>
        /// </summary>
        /// <param name="repository">Mercurial.Net <see cref="Repository"/></param>
        public HgRepository(Repository repository)
        {
            _repository = repository;
        }

        /// <inheritdoc />
        public string Path => _repository.Path;

        /// <inheritdoc />
        public IEnumerable<ICommit> Log(ILogQuery query)
        {
            var hgQuery = CastTo<HgLogQuery>(query);

            return _repository
                .Log(new LogCommand()
                    .WithRevision(hgQuery.Revision))
                .Select(changeset => (HgCommit) changeset);
        }

        /// <inheritdoc />
        public IEnumerable<ICommit> Log(Func<ILogQueryBuilder, ILogQuery> select)
        {
            if (select == null)
                throw new ArgumentNullException(nameof(select));

            var builder = new HgLogQueryBuilder();
            var query = select(builder);

            return Log(query);
        }
        
        /// <inheritdoc />
        public int Count(ILogQuery query)
        {
            var hgQuery = CastTo<HgLogQuery>(query);
            var command = new CountCommand()
                .WithRevision(hgQuery.Revision);

            return _repository.Execute(command);
        }

        /// <inheritdoc />
        public int Count(Func<ILogQueryBuilder, ILogQuery> select)
        {
            if (select == null)
                throw new ArgumentNullException(nameof(select));
            
            var builder = new HgLogQueryBuilder();
            var query = select(builder);

            return Count(query);
        }

        /// <inheritdoc />
        public IEnumerable<ICommit> Heads()
        {
            return _repository
                .Heads()
                .Select(changeset => (HgCommit) changeset);
        }

        /// <inheritdoc />
        public IBranchHead CurrentBranch()
        {
            var branchName = _repository.Branch();
            return new HgBranchHead(
                branchName,
                () => GetBranchHead(branchName));
        }

        /// <inheritdoc />
        public ICommit CurrentCommit()
        {
            var id = _repository.Identify();
            var revision = new HgLogQuery(RevSpec.To(id))
                .ExceptTaggingCommits()
                .Last()
                .Revision;
            
            return GetCommit(revision);
        }

        /// <inheritdoc />
        public IEnumerable<IBranchHead> Branches()
        {
            return _repository
                .Branches()
                .Select(head => new HgBranchHead(
                    head.Name,
                    () => GetCommit(head.RevisionNumber)));
        }

        /// <inheritdoc />
        public void Tag(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));

            _repository.Tag(name);
        }

        /// <inheritdoc />
        public void AddRemove()
        {
            _repository.AddRemove();
        }

        /// <inheritdoc />
        public string Commit(string message)
        {
            if (string.IsNullOrEmpty(message))
                throw new ArgumentNullException(nameof(message));

            return _repository.Commit(message);
        }

        /// <inheritdoc />
        public IEnumerable<ICommit> Parents(ICommit commit)
        {
            var hgCommit = CastTo<HgCommit>(commit);
            var command = new ParentsCommand()
                .WithRevision(hgCommit);

            return _repository.Parents(command)
                .Select(changeset => (HgCommit) changeset);
        }
        
        /// <inheritdoc />
        public ICommit GetCommit(int revisionNumber)
        {
            var id = _repository.Identify(new IdentifyCommand()
                .WithRevision(revisionNumber));

            return GetCommit(id);
        }

        /// <inheritdoc />
        public ICommit GetCommit(RevSpec revision)
        {
            var query = new HgLogQuery(revision)
                .Last();

            return (HgCommit) Log(query).First();
        }

        /// <inheritdoc />
        public ICommit GetBranchHead(string branchName)
        {
            var heads = _repository.Heads(new HeadsCommand()
                .WithBranchRevision(RevSpec.ByBranch(branchName)));

            return (HgCommit) heads.First();
        }

        /// <inheritdoc />
        public void Branch(string branch)
        {
            _repository.Branch(branch);
        }

        /// <inheritdoc />
        public void Update(RevSpec rev)
        {
            _repository.Update(rev);
        }

        /// <summary>
        /// Converts a <see cref="Repository"/> into a <see cref="HgRepository"/>
        /// </summary>
        /// <param name="repository">Mercurial.Net <see cref="Repository"/></param>
        /// <returns></returns>
        public static implicit operator HgRepository(Repository repository) =>
            new HgRepository(repository);

        private static T CastTo<T>(object from)
        {
            if (from == null)
                throw new ArgumentNullException(nameof(from));

            if (!(from is T to))
                throw new InvalidCastException($"{from.GetType()} is not supported.");

            return to;
        }
    }
}